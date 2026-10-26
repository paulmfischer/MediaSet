"""Subprocess contract fixtures; grants and environment paths are disposable."""
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import subprocess
import shutil
import tempfile
import time
import tomllib
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]


class EnvGuardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="environment guard ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.repo = self.root / "checkout with spaces"
        self.repo.mkdir()
        self.env = dict(os.environ, HOME=str(self.root), GIT_CONFIG_NOSYSTEM="1",
                        GIT_CONFIG_GLOBAL=os.devnull)
        for key in list(self.env):
            if key.startswith(("GIT_CONFIG_KEY_", "GIT_CONFIG_VALUE_")) or key in {
                "GIT_CONFIG_COUNT", "GIT_CONFIG_PARAMETERS", "GIT_DIR", "GIT_WORK_TREE"
            }:
                self.env.pop(key)
        self.git("init", "-b", "main")
        self.git("-c", "user.name=Fixture", "-c", "user.email=fixture@example.invalid",
                 "-c", "commit.gpgsign=false", "commit", "--allow-empty", "-m", "fixture")
        (self.repo / "sub directory").mkdir()

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.repo, env=self.env,
                              capture_output=True, text=True, check=True).stdout.strip()

    def payload(self, runtime, path=".env", operation="update", cwd=None, destination=None):
        data = {"cwd": str(cwd or self.repo), "hook_event_name": "PreToolUse"}
        if runtime == "claude":
            data.update(tool_name="Write" if operation == "create" else "Edit",
                        tool_input={"file_path": path, "content": "SECRET_SENTINEL",
                                    "old_string": "SECRET_SENTINEL", "new_string": "SECRET_SENTINEL"})
        else:
            kind = {"create": "Add", "update": "Update", "delete": "Delete", "move": "Update"}[operation]
            patch = f"*** Begin Patch\n*** {kind} File: {path}\n"
            if destination:
                patch += f"*** Move to: {destination}\n"
            if kind == "Add":
                patch += "+SECRET_SENTINEL\n"
            elif kind == "Update":
                patch += "@@\n-SECRET_SENTINEL\n+SECRET_SENTINEL\n"
            data.update(tool_name="apply_patch", tool_input={"command": patch + "*** End Patch"})
        return data

    def invoke(self, runtime, payload, blocked, cwd=None):
        raw = payload if isinstance(payload, str) else json.dumps(payload)
        result = subprocess.run(["python3", "-B", str(SCRIPTS / f"{runtime}_env_guard.py")],
                                cwd=cwd or self.root, env=self.env, input=raw,
                                text=True, capture_output=True, check=True, timeout=10)
        self.assertEqual(result.stderr, "")
        self.assertNotIn("SECRET_SENTINEL", result.stdout)
        if blocked:
            self.assertEqual(json.loads(result.stdout), {"hookSpecificOutput": {
                "hookEventName": "PreToolUse", "permissionDecision": "deny",
                "permissionDecisionReason": "Environment edit blocked. See Development/AGENT_ENV_GUARD.md for "
                "classification and a temporary file/operation exception after explicit user authorization."}})
        else:
            self.assertEqual(result.stdout, "")
        return result.stdout

    def grant(self, path=".env", operation="update", cwd=None, ttl=300, success=True):
        result = subprocess.run(["python3", "-B", str(SCRIPTS / "env_guard.py"),
                                 "--file", path, "--operation", operation, "--ttl", str(ttl), "--authorized"],
                                cwd=cwd or self.repo, env=self.env, text=True, capture_output=True, timeout=10)
        self.assertEqual(result.returncode, 0 if success else 1, result.stderr)
        self.assertNotIn("SECRET_SENTINEL", result.stdout + result.stderr)

    def test_default_classification_through_both_adapters(self):
        for runtime in ("claude", "codex"):
            for path in (".env", ".env.local", ".env.production", "sub directory/.env.secret"):
                with self.subTest(runtime=runtime, path=path):
                    self.invoke(runtime, self.payload(runtime, path), True)
            for path in (".env.example", ".env.sample", ".env.template", ".env.production.example",
                         "sub directory/.env.local.template", "README.md", "my.env", "environment.py"):
                with self.subTest(runtime=runtime, path=path):
                    self.invoke(runtime, self.payload(runtime, path), False)

    def test_templates_do_not_open_similarly_named_actual_files(self):
        for runtime in ("claude", "codex"):
            for path in (".env.example.local", ".env.examples", ".env.template.backup"):
                self.invoke(runtime, self.payload(runtime, path), True)

    def test_codex_add_update_delete_and_move_endpoints(self):
        for operation in ("create", "update", "delete"):
            self.invoke("codex", self.payload("codex", operation=operation), True)
            self.invoke("codex", self.payload("codex", ".env.example", operation), False)
        for source, destination in ((".env", "safe.txt"), ("safe.txt", ".env"), (".env", ".env.local")):
            self.invoke("codex", self.payload("codex", source, "move", destination=destination), True)
        self.invoke("codex", self.payload("codex", "safe.txt", "move", destination=".env.example"), False)

    def test_codex_create_and_delete_grants(self):
        for operation in ("create", "delete"):
            self.grant(operation=operation)
            payload = self.payload("codex", operation=operation)
            self.invoke("codex", payload, False)
            self.invoke("codex", payload, True)

    def test_claude_write_create_and_overwrite(self):
        payload = self.payload("claude", operation="create")
        self.grant(operation="create")
        self.invoke("claude", payload, False)
        (self.repo / ".env").touch()
        self.grant(operation="create")
        self.invoke("claude", payload, True)
        self.grant(operation="update")
        self.invoke("claude", payload, False)

    def test_native_write_and_add_overwrites_require_update_grant(self):
        for runtime in ("claude", "codex"):
            with self.subTest(runtime=runtime):
                path = self.repo / ".env"
                path.touch()
                payload = self.payload(runtime, operation="create")
                self.grant(operation="create")
                self.invoke(runtime, payload, True)
                self.grant(operation="update")
                self.invoke(runtime, payload, False)
                self.invoke(runtime, payload, True)
                # Denial of the overwrite leaves the create grant available,
                # but it can only authorize a genuinely missing destination.
                path.unlink()
                self.invoke(runtime, payload, False)
                self.invoke(runtime, payload, True)

    def test_codex_multi_file_adds_classify_existing_destinations(self):
        (self.repo / ".env").touch()
        payload = self.payload("codex", operation="create")
        second = self.payload("codex", ".env.local", "create")["tool_input"]["command"]
        payload["tool_input"]["command"] = payload["tool_input"]["command"].removesuffix("*** End Patch") + second.removeprefix("*** Begin Patch\n")
        self.grant(operation="create")
        self.grant(".env.local", "create")
        self.invoke("codex", payload, True)
        self.grant(operation="update")
        self.invoke("codex", payload, False)
        self.invoke("codex", payload, True)

    def test_codex_second_add_to_same_missing_file_requires_update_too(self):
        payload = self.payload("codex", operation="create")
        command = payload["tool_input"]["command"]
        payload["tool_input"]["command"] = command.removesuffix("*** End Patch") + command.removeprefix("*** Begin Patch\n")
        self.grant(operation="create")
        self.invoke("codex", payload, True)
        self.grant(operation="update")
        self.invoke("codex", payload, False)
        self.invoke("codex", payload, True)

    def test_codex_first_update_hunk_can_omit_header(self):
        for path in ("safe.txt", ".env.example", ".env"):
            for suffix in ("", "@@ skip-ahead context\n-old\n+new\n", "*** End of File\n"):
                with self.subTest(path=path, suffix=suffix):
                    payload = self.payload("codex", path)
                    payload["tool_input"]["command"] = (
                        f"*** Begin Patch\n*** Update File: {path}\n-old\n+new\n"
                        + suffix + "*** End Patch")
                    if path == ".env":
                        self.invoke("codex", payload, True)
                        self.grant()
                    self.invoke("codex", payload, False)

    def test_codex_move_first_hunk_can_omit_header(self):
        for source, destination in (("safe.txt", "renamed.txt"), (".env", ".env.local")):
            payload = self.payload("codex", source, "move", destination=destination)
            payload["tool_input"]["command"] = payload["tool_input"]["command"].replace("@@\n", "", 1)
            if source == ".env":
                self.invoke("codex", payload, True)
                self.grant(operation="move-from")
                self.grant(".env.local", "move-to")
            self.invoke("codex", payload, False)

    def test_codex_malformed_hunks_do_not_consume_exception(self):
        self.grant()
        for body in ("", "@@\n", "@@bad\n-old\n+new\n", "@@\n@@\n-old\n+new\n",
                     "-old\n+new\n@@\n", "*** End of File\n", "-old\n*** End of File\n+new\n"):
            payload = self.payload("codex")
            payload["tool_input"]["command"] = "*** Begin Patch\n*** Update File: .env\n" + body + "*** End Patch"
            self.invoke("codex", payload, True)
        self.invoke("codex", self.payload("codex"), False)

    def test_paths_use_payload_cwd_not_adapter_process_directory(self):
        sub = self.repo / "sub directory"
        for runtime in ("claude", "codex"):
            for path in ("../.env", str(self.repo / ".env"), "../sub directory/../.env"):
                self.invoke(runtime, self.payload(runtime, path, cwd=sub), True)
            self.grant("../.env", cwd=sub)
            self.invoke(runtime, self.payload(runtime, str(self.repo / ".env"), cwd=sub), False)

    def test_symlinks_classify_both_requested_and_resolved_names(self):
        (self.repo / "alias.txt").symlink_to(".env")
        (self.repo / ".env.example").symlink_to(".env")
        (self.repo / ".env").symlink_to("safe.txt")
        for runtime in ("claude", "codex"):
            for path in ("alias.txt", ".env.example", ".env"):
                # alias.txt ends at safe.txt, but .env.example resolves through .env too.
                self.invoke(runtime, self.payload(runtime, path), path == ".env")
        (self.repo / ".env").unlink()
        for runtime in ("claude", "codex"):
            for path in ("alias.txt", ".env.example"):
                self.invoke(runtime, self.payload(runtime, path), True)
        (self.repo / "env directory").mkdir()
        (self.repo / "linked directory").symlink_to("env directory", target_is_directory=True)
        self.grant("env directory/.env")
        self.invoke("codex", self.payload("codex", "linked directory/.env"), True)
        self.invoke("codex", self.payload("codex", "env directory/.env"), False)

    def test_protected_symlink_grants_and_native_edits_require_direct_paths(self):
        (self.repo / ".env").touch()
        (self.repo / "alias.txt").symlink_to(".env")
        (self.repo / "linked directory").symlink_to("sub directory", target_is_directory=True)
        for runtime in ("claude", "codex"):
            self.grant("alias.txt", success=False)
            self.grant("linked directory/.env", success=False)
            self.grant()
            self.invoke(runtime, self.payload(runtime, "alias.txt"), True)
            self.invoke(runtime, self.payload(runtime), False)

    def test_alias_unlink_then_real_target_add_cannot_use_create_grant(self):
        (self.repo / ".env").touch()
        (self.repo / "alias.txt").symlink_to(".env")
        payload = self.payload("codex", "alias.txt", "delete")
        add = self.payload("codex", operation="create")["tool_input"]["command"]
        payload["tool_input"]["command"] = payload["tool_input"]["command"].removesuffix("*** End Patch") + add.removeprefix("*** Begin Patch\n")
        self.grant(operation="delete")
        self.grant(operation="create")
        self.invoke("codex", payload, True)
        self.invoke("codex", self.payload("codex", operation="create"), True)
        self.grant(operation="update")
        self.invoke("codex", self.payload("codex", operation="create"), False)

    def test_symlink_move_endpoints_are_denied_even_with_canonical_grants(self):
        (self.repo / ".env").touch()
        (self.repo / "alias.txt").symlink_to(".env")
        for source, destination in (("alias.txt", "safe.txt"), ("safe.txt", "alias.txt"),
                                    ("alias.txt", ".env.local"), (".env.local", "alias.txt")):
            with self.subTest(source=source, destination=destination):
                for path in (".env", ".env.local"):
                    for operation in ("move-from", "move-to"):
                        self.grant(path, operation)
                self.invoke("codex", self.payload("codex", source, "move", destination=destination), True)
        payload = self.payload("codex", ".env", "move", destination=".env.local")
        self.invoke("codex", payload, False)

    def test_safe_symlinks_abstain_but_mixed_protected_patch_requires_split(self):
        (self.repo / "safe.txt").touch()
        (self.repo / "safe alias.txt").symlink_to("safe.txt")
        (self.repo / ".env").touch()
        for runtime in ("claude", "codex"):
            self.invoke(runtime, self.payload(runtime, "safe alias.txt"), False)
        for operation in ("delete", "move"):
            payload = self.payload("codex", "safe alias.txt", operation,
                                   destination="renamed.txt" if operation == "move" else None)
            self.invoke("codex", payload, False)
            add = self.payload("codex", operation="create")["tool_input"]["command"]
            payload["tool_input"]["command"] = payload["tool_input"]["command"].removesuffix("*** End Patch") + add.removeprefix("*** Begin Patch\n")
            self.grant(operation="create")
            self.grant(operation="update")
            self.invoke("codex", payload, True)
            self.invoke("codex", self.payload("codex", operation="create"), False)

    def test_grants_are_single_use_for_both_adapters(self):
        for runtime in ("claude", "codex"):
            self.grant()
            payload = self.payload(runtime)
            self.invoke(runtime, payload, False)
            self.invoke(runtime, payload, True)

    def test_grants_do_not_apply_to_other_file_or_operation(self):
        for runtime in ("claude", "codex"):
            self.grant()
            self.invoke(runtime, self.payload(runtime, ".env.local"), True)
            self.invoke(runtime, self.payload(runtime, operation="create"), True)
            self.invoke(runtime, self.payload(runtime), False)

    def test_expiration_does_not_authorize_later_edits(self):
        for runtime in ("claude", "codex"):
            self.grant(ttl=1)
            time.sleep(1.05)
            self.invoke(runtime, self.payload(runtime), True)

    def test_multi_file_patch_consumes_all_grants_only_when_entire_request_allowed(self):
        first = self.payload("codex")["tool_input"]["command"]
        second = self.payload("codex", ".env.local")["tool_input"]["command"]
        payload = self.payload("codex")
        payload["tool_input"]["command"] = first.removesuffix("*** End Patch") + second.removeprefix("*** Begin Patch\n")
        self.grant()
        self.invoke("codex", payload, True)
        self.grant(".env.local")
        self.invoke("codex", payload, False)
        self.invoke("codex", payload, True)

    def test_moves_require_exact_operation_grants_for_each_protected_endpoint(self):
        payload = self.payload("codex", ".env", "move", destination=".env.local")
        self.grant()
        self.grant(".env.local")
        self.invoke("codex", payload, True)
        self.grant(operation="move-from")
        self.invoke("codex", payload, True)
        self.grant(".env.local", "move-to")
        self.invoke("codex", payload, False)

    def test_malformed_native_payloads_fail_closed_without_secrets(self):
        for runtime in ("claude", "codex"):
            payload = self.payload(runtime)
            for raw in ("{SECRET_SENTINEL", "[]", json.dumps(payload).replace('"tool_input": {', '"tool_input": null, "ignored": {')):
                self.invoke(runtime, raw, True)
            for path in ("", None, 42, ".env\nSECRET_SENTINEL", ".env\x00"):
                malformed = self.payload(runtime)
                if runtime == "claude":
                    malformed["tool_input"]["file_path"] = path
                else:
                    malformed["tool_input"]["command"] = path
                self.invoke(runtime, malformed, True)
            payload.pop("cwd")
            self.invoke(runtime, payload, True)
            self.invoke(runtime, "X" * (2 * 1024 * 1024 + 1), True)

    def test_malformed_patch_does_not_consume_grant(self):
        self.grant()
        valid = self.payload("codex")
        for command in ("*** Begin Patch\n*** Update File: .env\nSECRET_SENTINEL\n*** End Patch",
                        valid["tool_input"]["command"].removesuffix("*** End Patch"),
                        "*** Begin Patch\n*** Delete File: .env\n+SECRET_SENTINEL\n*** End Patch"):
            payload = self.payload("codex")
            payload["tool_input"]["command"] = command
            self.invoke("codex", payload, True)
        self.invoke("codex", valid, False)

    def test_unrelated_tools_abstain_without_reading_their_arguments(self):
        for runtime in ("claude", "codex"):
            self.invoke(runtime, {"tool_name": "Bash", "tool_input": {"command": "echo SECRET_SENTINEL"}}, False)

    def test_grant_helper_limits_and_no_implicit_authorization(self):
        for ttl in (0, -1, 601):
            self.grant(ttl=ttl, success=False)
        self.grant(".env.example", success=False)
        self.grant(str(self.root / ".env"), success=False)
        result = subprocess.run(["python3", "-B", str(SCRIPTS / "env_guard.py"), "--file", ".env", "--operation", "update"],
                                cwd=self.repo, env=self.env, capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse((self.repo / ".agent-local").exists())

    def test_state_is_ignored_private_and_contains_no_values(self):
        self.git("config", "core.excludesFile", "/dev/null")
        (self.repo / ".gitignore").write_text(".agent-local/\n")
        self.grant()
        state = self.repo / ".agent-local/env-guard/exceptions.json"
        self.assertEqual(state.stat().st_mode & 0o777, 0o600)
        records = json.loads(state.read_text())
        self.assertEqual(set(records[0]), {"path", "operation", "expires"})
        self.assertNotIn("SECRET_SENTINEL", state.read_text())
        self.git("check-ignore", ".agent-local/env-guard/exceptions.json")

    def test_corrupt_and_symlinked_state_fail_closed(self):
        self.grant()
        state = self.repo / ".agent-local/env-guard/exceptions.json"
        for text in ("{SECRET_SENTINEL", "[{}]", '[{"path":1,"operation":"update","expires":0}]',
                     json.dumps([{"path": str(self.repo / ".env"), "operation": "update", "expires": 10 ** 1000}])):
            state.write_text(text)
            self.invoke("codex", self.payload("codex"), True)
        state.unlink()
        state.symlink_to(self.root / "never-read")
        self.invoke("codex", self.payload("codex"), True)

    def test_parallel_requests_cannot_reuse_grant(self):
        self.grant()
        payload = json.dumps(self.payload("codex"))
        def invoke(_):
            return subprocess.run(["python3", "-B", str(SCRIPTS / "codex_env_guard.py")],
                                  cwd=self.root, env=self.env, input=payload, text=True,
                                  capture_output=True, check=True, timeout=10).stdout
        with ThreadPoolExecutor(max_workers=6) as executor:
            outputs = list(executor.map(invoke, range(6)))
        self.assertEqual(outputs.count(""), 1)
        self.assertEqual(sum('"deny"' in output for output in outputs), 5)

    def test_worktrees_and_alternate_checkouts_have_independent_grants(self):
        worktree = self.root / "worktree with spaces"
        self.git("worktree", "add", "-b", "fixture", str(worktree))
        other = self.root / "alternate checkout"
        self.git("clone", str(self.repo), str(other))
        self.grant()
        for checkout in (worktree, other):
            for runtime in ("claude", "codex"):
                self.invoke(runtime, self.payload(runtime, cwd=checkout), True)
                self.grant(cwd=checkout)
                self.invoke(runtime, self.payload(runtime, cwd=checkout), False)
        self.invoke("codex", self.payload("codex"), False)

    def test_tracked_native_registrations_execute_in_checkout_with_spaces(self):
        source = SCRIPTS.parents[1]
        claude = json.loads((source / ".claude/settings.json").read_text())
        codex = tomllib.loads((source / ".codex/config.toml").read_text())
        destination = self.repo / "scripts/agents"
        destination.mkdir(parents=True)
        for name in ("env_guard.py", "claude_env_guard.py", "codex_env_guard.py"):
            shutil.copyfile(SCRIPTS / name, destination / name)
        for runtime, config in (("claude", claude), ("codex", codex)):
            groups = config["hooks"]["PreToolUse"]
            entries = [(group["matcher"], hook["command"]) for group in groups
                       for hook in group["hooks"] if "env_guard.py" in hook["command"]]
            self.assertEqual(len(entries), 1)
            matcher, command = entries[0]
            self.assertEqual(matcher, "Write|Edit" if runtime == "claude" else "apply_patch")
            self.assertFalse(any("protect-files.sh" in hook["command"] for group in groups for hook in group["hooks"]))
            result = subprocess.run(command, shell=True, cwd=self.repo / "sub directory",
                                    env=dict(self.env, CLAUDE_PROJECT_DIR=str(self.repo)),
                                    input=json.dumps(self.payload(runtime)), capture_output=True, text=True, timeout=10)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(result.stderr, "")
            self.assertEqual(json.loads(result.stdout)["hookSpecificOutput"]["permissionDecision"], "deny")


if __name__ == "__main__":
    unittest.main()
