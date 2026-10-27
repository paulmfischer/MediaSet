"""Behavioral fixtures: only disposable checkouts and fake package managers run."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


SOURCE = Path(__file__).resolve().parents[3]
GIT = shutil.which("git")
BASH = shutil.which("bash")

STUB = '''import json, os, pathlib, sys
name = pathlib.Path(sys.argv[0]).name
args = sys.argv[1:]
if args == ["--version"]:
    print({"node": os.environ.get("TEST_NODE_VERSION", "v24.22.1"),
           "npm": "10.0.0", "dotnet": "10.0.112"}[name])
    sys.exit(0)
with open(os.environ["TEST_CALLS"], "a") as stream:
    stream.write(json.dumps({"name": name, "args": args, "cwd": os.getcwd(),
        "nuget": os.environ.get("NUGET_PACKAGES"),
        "cache": os.environ.get("npm_config_cache")}) + "\\n")
if os.environ.get("TEST_FAIL") == name:
    print("fixture dependency failure", file=sys.stderr)
    sys.exit(7)
root = pathlib.Path.cwd()
if name == "dotnet":
    for project in ("MediaSet.Api", "MediaSet.Api.Analyzers", "MediaSet.Api.Tests"):
        dest = root / project / "obj/project.assets.json"
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text("{}")
else:
    (root / "node_modules").mkdir(exist_ok=True)
'''


@unittest.skipUnless(GIT and BASH, "Git and Bash required for checkout fixtures")
class SetupBehaviorTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="agent setup ")
        self.addCleanup(self.temp.cleanup)
        self.base = Path(self.temp.name)
        self.root = self.base / "checkout with spaces"
        self.root.mkdir()
        self.home = self.base / "personal"
        self.home.mkdir()
        self.bin = self.base / "tools"
        self.bin.mkdir()
        self.calls = self.base / "calls.jsonl"
        self.env = {**os.environ, "HOME": str(self.home), "TEST_CALLS": str(self.calls),
                    "GIT_CONFIG_GLOBAL": os.devnull, "GIT_CONFIG_NOSYSTEM": "1",
                    "CODEX_HOME": str(self.home / ".codex"),
                    "CLAUDE_CONFIG_DIR": str(self.home / ".claude"),
                    "PATH": str(self.bin), "PYTHONDONTWRITEBYTECODE": "1"}
        for name in ("node", "npm", "dotnet"):
            path = self.bin / name
            path.write_text("#!" + sys.executable + "\n" + STUB)
            path.chmod(0o755)
        for name, executable in (("git", GIT), ("bash", BASH), ("python3", sys.executable)):
            (self.bin / name).symlink_to(executable)
        shutil.copytree(SOURCE / "scripts/agents", self.root / "scripts/agents",
                        ignore=shutil.ignore_patterns("__pycache__"))
        (self.root / "MediaSet.sln").write_text("fixture")
        for name in ("MediaSet.Api", "MediaSet.Api.Analyzers", "MediaSet.Api.Tests", "MediaSet.Remix"):
            (self.root / name).mkdir()
            (self.root / name / "placeholder").write_text("fixture")
        (self.root / "global.json").write_text('{"sdk":{"version":"10.0.101","rollForward":"latestFeature"}}')
        (self.root / ".gitignore").write_text(".agent-local/\nnode_modules/\nobj/\n")
        self.git("init", "-b", "fixture")
        self.git("config", "user.name", "Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.git("add", ".")
        self.git("commit", "-m", "fixture")

    def git(self, *args):
        return subprocess.run([GIT, "-C", str(self.root), *args], env=self.env,
                              capture_output=True, text=True, check=True)

    def run_setup(self, command, root=None, cwd=None):
        root = root or self.root
        return subprocess.run([BASH, str(root / "scripts/agents/setup.sh"), command],
                              cwd=cwd or root, env=self.env, capture_output=True, text=True)

    def installed_calls(self):
        return [json.loads(line) for line in self.calls.read_text().splitlines()] if self.calls.exists() else []

    def settings(self, path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(data))

    def test_diagnose_is_passive_and_reports_unverified_missing_dependencies(self):
        before = self.git("status", "--porcelain").stdout
        result = self.run_setup("diagnose", cwd=self.root / "MediaSet.Remix")
        self.assertEqual(1, result.returncode, result.stdout + result.stderr)
        self.assertIn("INCOMPLETE: missing MediaSet.Remix/node_modules", result.stdout)
        self.assertIn("Hook activation: unverified", result.stdout)
        self.assertIn("unverified", result.stdout)
        self.assertEqual([], self.installed_calls())
        self.assertEqual(before, self.git("status", "--porcelain").stdout)
        self.assertFalse((self.root / ".agent-local").exists())

    def test_bootstrap_is_repeatable_scoped_and_preserves_personal_settings(self):
        personal = self.home / ".codex/config.toml"
        personal.parent.mkdir()
        personal.write_text('private_value = "DO_NOT_PRINT"\n')
        candidate = self.root / ".codex/hooks.json"
        self.settings(candidate, {"hooks": {}, "private_value": "DO_NOT_PRINT"})
        snapshot = (personal.read_bytes(), candidate.read_bytes())
        for _ in range(2):
            result = self.run_setup("bootstrap", cwd=self.root / "MediaSet.Api")
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        calls = self.installed_calls()
        self.assertEqual(["dotnet", "npm", "dotnet", "npm"], [x["name"] for x in calls])
        self.assertEqual(["restore", "MediaSet.sln"], calls[0]["args"])
        self.assertEqual(str(self.root), calls[0]["cwd"])
        self.assertEqual(str(self.root / "MediaSet.Remix"), calls[1]["cwd"])
        self.assertEqual(str(self.root / ".agent-local/nuget"), calls[0]["nuget"])
        self.assertEqual(str(self.root / ".agent-local/npm-cache"), calls[1]["cache"])
        result = self.run_setup("diagnose")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertNotIn("DO_NOT_PRINT", result.stdout + result.stderr)
        self.assertIn("untracked Codex candidate preserved", result.stdout)
        self.assertEqual(snapshot, (personal.read_bytes(), candidate.read_bytes()))

    def test_missing_tool_and_old_node_prevent_all_dependency_commands(self):
        (self.bin / "npm").unlink()
        self.env["TEST_NODE_VERSION"] = "v18.20.0"
        result = self.run_setup("bootstrap")
        self.assertEqual(1, result.returncode)
        self.assertIn("FAILED: npm", result.stdout)
        self.assertIn("FAILED: node", result.stdout)
        self.assertIn("#prerequisites", result.stdout)
        self.assertEqual([], self.installed_calls())

    def test_node_lts_patch_floor_and_major_prevent_dependency_commands(self):
        for version in ("v22.23.4", "v24.3.0", "v24.22.0", "v25.0.0", "v26.0.0"):
            with self.subTest(version=version):
                self.env["TEST_NODE_VERSION"] = version
                result = self.run_setup("bootstrap")
                self.assertEqual(1, result.returncode, result.stdout + result.stderr)
                self.assertIn("FAILED: node", result.stdout)
                self.assertEqual([], self.installed_calls())

    def test_newer_node_24_patch_is_accepted(self):
        self.env["TEST_NODE_VERSION"] = "v24.22.2"
        result = self.run_setup("bootstrap")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)

    def test_launcher_explains_missing_python(self):
        (self.bin / "python3").unlink()
        result = self.run_setup("diagnose")
        self.assertEqual(1, result.returncode)
        self.assertIn("Python >=3.11 required", result.stderr)
        self.assertIn("sudo apt-get install python3", result.stderr)

    def test_restore_failure_is_visible_stops_npm_and_preserves_status(self):
        self.env["TEST_FAIL"] = "dotnet"
        result = self.run_setup("bootstrap")
        self.assertEqual(7, result.returncode)
        self.assertIn("fixture dependency failure", result.stderr)
        self.assertEqual(["dotnet"], [x["name"] for x in self.installed_calls()])

    def test_npm_failure_is_visible_and_can_be_rerun(self):
        self.env["TEST_FAIL"] = "npm"
        result = self.run_setup("bootstrap")
        self.assertEqual(7, result.returncode)
        self.assertIn("FAILED: npm", result.stdout)
        del self.env["TEST_FAIL"]
        self.assertEqual(0, self.run_setup("bootstrap").returncode)

    def test_bootstrap_refuses_dependency_and_cache_symlinks(self):
        for relative in ("MediaSet.Remix/node_modules", ".agent-local"):
            with self.subTest(relative=relative):
                target = self.base / "shared"
                target.mkdir(exist_ok=True)
                sentinel = target / "preserve"
                sentinel.write_text("unchanged")
                link = self.root / relative
                link.symlink_to(target, target_is_directory=True)
                result = self.run_setup("bootstrap")
                self.assertEqual(1, result.returncode)
                self.assertIn("refuses", result.stdout)
                self.assertEqual([], self.installed_calls())
                self.assertEqual("unchanged", sentinel.read_text())
                link.unlink()

    def test_worktree_owns_dependencies_even_when_invoked_from_other_checkout(self):
        worktree = self.base / "independent worktree"
        self.git("worktree", "add", "-b", "second", str(worktree))
        result = self.run_setup("bootstrap", root=worktree, cwd=self.root / "MediaSet.Api")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertTrue((worktree / "MediaSet.Remix/node_modules").is_dir())
        self.assertFalse((self.root / "MediaSet.Remix/node_modules").exists())
        self.assertEqual(str(worktree / ".agent-local/nuget"), self.installed_calls()[0]["nuget"])

    def test_duplicate_json_toml_registration_detected_without_secret_disclosure(self):
        entry = [{"matcher": "Bash", "hooks": [{"command": "DO_NOT_PRINT", "type": "command"}]}]
        self.settings(self.root / ".codex/hooks.json", {"hooks": {"PreToolUse": entry}})
        toml = self.home / ".codex/config.toml"
        toml.parent.mkdir()
        toml.write_text('[features]\nhooks = false\n[[hooks.PreToolUse]]\nmatcher = "Bash"\n'
                        '[[hooks.PreToolUse.hooks]]\ncommand = "DO_NOT_PRINT"\ntype = "command"\n')
        result = self.run_setup("diagnose")
        self.assertIn("duplicate", result.stdout)
        self.assertIn("disabled in this file", result.stdout)
        self.assertIn("active/trusted unverified", result.stdout)
        self.assertNotIn("DO_NOT_PRINT", result.stdout + result.stderr)

    def test_legacy_and_shared_environment_guards_warn_without_changing_settings(self):
        for runtime, directory in (("Claude", ".claude"), ("Codex", ".codex")):
            path = self.root / directory / ("settings.json" if runtime == "Claude" else "hooks.json")
            data = {"hooks": {"PreToolUse": [{"matcher": "Write|Edit", "hooks": [
                {"type": "command", "command": "protect-files.sh DO_NOT_PRINT"},
                {"type": "command", "command": "python3 env_guard.py DO_NOT_PRINT"}]}]}}
            self.settings(path, data)
            before = path.read_bytes()
            result = self.run_setup("diagnose")
            self.assertIn(runtime + " old/new environment guards compete", result.stdout)
            self.assertIn("active/trusted unverified", result.stdout)
            self.assertNotIn("DO_NOT_PRINT", result.stdout + result.stderr)
            self.assertEqual(before, path.read_bytes())

    def test_invalid_settings_never_echo_contents(self):
        path = self.root / ".claude/settings.local.json"
        path.parent.mkdir()
        path.write_text("DO_NOT_PRINT invalid JSON")
        result = self.run_setup("diagnose")
        self.assertIn("unreadable/invalid settings", result.stdout)
        self.assertNotIn("DO_NOT_PRINT", result.stdout + result.stderr)

    def test_retired_hooks_warn_even_without_shared_guard_and_preserve_personal_settings(self):
        for name in ("block-main-commits.sh", "protect-files.sh", "eslint-fix.sh",
                     "prettier-format.sh", "filter-test-output.sh", "verify-tests.sh"):
            with self.subTest(name=name):
                path = self.home / ".codex/hooks.json"
                self.settings(path, {"private": "DO_NOT_PRINT", "hooks": {
                    "Stop" if name == "verify-tests.sh" else "PreToolUse": [
                        {"matcher": "Bash", "hooks": [{"type": "command",
                          "command": "bash " + name + " DO_NOT_PRINT"}]}]}})
                before = path.read_bytes()
                result = self.run_setup("diagnose")
                self.assertIn("Codex obsolete project-style hook registrations detected", result.stdout)
                self.assertNotIn("DO_NOT_PRINT", result.stdout + result.stderr)
                self.assertEqual(before, path.read_bytes())

    def test_migrated_tracked_definitions_have_only_two_pretool_guards(self):
        import tomllib
        definitions = [json.loads((SOURCE / ".claude/settings.json").read_text()),
                       tomllib.loads((SOURCE / ".codex/config.toml").read_text())]
        for data in definitions:
            self.assertEqual({"PreToolUse"}, set(data["hooks"]))
            groups = data["hooks"]["PreToolUse"]
            self.assertEqual(2, len(groups))
            commands = [hook["command"] for group in groups for hook in group["hooks"]]
            self.assertEqual(2, len(commands))
            self.assertEqual(1, sum("git_guard.py" in c for c in commands))
            self.assertEqual(1, sum("env_guard.py" in c for c in commands))
            for retired in ("eslint-fix", "prettier-format", "filter-test-output", "verify-tests",
                            "block-main-commits", "protect-files"):
                self.assertFalse(any(retired in command for command in commands))

    def test_claude_launcher_uses_own_checkout_and_preserves_arguments_and_settings(self):
        runtime = self.bin / "claude"
        runtime.write_text("#!" + sys.executable + "\nimport json,os,sys\n"
                           "print(json.dumps({'cwd':os.getcwd(),'args':sys.argv[1:]}))\n")
        runtime.chmod(0o755)
        personal = self.home / ".claude/settings.json"
        self.settings(personal, {"private": "DO_NOT_PRINT"})
        before = personal.read_bytes()
        worktree = self.base / "launcher independent worktree"
        self.git("worktree", "add", "-b", "launcher", str(worktree))
        args = ["prompt with spaces; $literal", "--settings", '{"disableAllHooks":true}']
        for checkout, caller in ((self.root, worktree / "MediaSet.Api"),
                                  (worktree, self.root / "MediaSet.Api")):
            result = subprocess.run([BASH, str(checkout / "scripts/agents/claude.sh"), *args],
                                    cwd=caller, env=self.env, capture_output=True, text=True)
            self.assertEqual(0, result.returncode, result.stderr)
            self.assertEqual({"cwd": str(checkout), "args": args}, json.loads(result.stdout))
        self.assertEqual(before, personal.read_bytes())
        self.assertEqual([], self.installed_calls())

    def test_claude_launcher_missing_optional_runtime_is_actionable(self):
        result = subprocess.run([BASH, str(self.root / "scripts/agents/claude.sh")],
                                cwd=self.root / "MediaSet.Api", env=self.env,
                                capture_output=True, text=True)
        self.assertEqual(1, result.returncode)
        self.assertIn("optional Claude Code runtime not found", result.stderr)
        self.assertEqual([], self.installed_calls())


if __name__ == "__main__":
    unittest.main()
