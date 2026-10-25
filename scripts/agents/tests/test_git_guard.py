"""External native-contract fixtures; every Git write is in a disposable repo."""
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


SCRIPTS = Path(__file__).resolve().parents[1]


class GitGuardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="git guard ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo with spaces"
        self.repo.mkdir()
        self.env = dict(os.environ, HOME=str(self.root), GIT_CONFIG_NOSYSTEM="1",
                        GIT_CONFIG_GLOBAL=os.devnull)
        for key in list(self.env):
            if key.startswith(("GIT_CONFIG_KEY_", "GIT_CONFIG_VALUE_")) or key in {"GIT_CONFIG_COUNT", "GIT_CONFIG_PARAMETERS", "GIT_DIR", "GIT_WORK_TREE"}:
                self.env.pop(key)
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.git("commit", "--allow-empty", "-m", "initial")
        self.git("switch", "-c", "feature/test")
        self.git("config", "commit.gpgsign", "true")
        self.git("config", "remote.origin.url", str(self.root / "fixture-remote.git"))
        self.git("config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*")
        self.git("config", "branch.main.remote", "origin")
        self.git("config", "branch.main.merge", "refs/heads/main")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")
        (self.repo / "subdir").mkdir()

    def git(self, *args):
        return subprocess.run(["git", *args], cwd=self.repo, env=self.env,
                              capture_output=True, text=True, check=True).stdout.strip()

    def check_command(self, command, blocked, cwd=None, tool_cwd=None):
        for runtime in ("claude", "codex"):
            with self.subTest(runtime=runtime, command=command):
                payload = {"hook_event_name": "PreToolUse", "tool_name": "Bash",
                           "cwd": str(cwd or self.repo), "tool_input": {"command": command}}
                if tool_cwd:
                    payload["tool_input"]["cwd"] = str(tool_cwd)
                result = subprocess.run(["python3", "-B", str(SCRIPTS / (runtime + "_git_guard.py"))],
                                        input=json.dumps(payload), env=self.env,
                                        cwd=self.root, capture_output=True, text=True, check=True)
                self.assertEqual(result.stderr, "")
                if blocked:
                    output = json.loads(result.stdout)
                    self.assertEqual(output["hookSpecificOutput"]["permissionDecision"], "deny")
                    self.assertEqual(set(output), {"hookSpecificOutput"})
                    self.assertNotIn("updatedInput", output["hookSpecificOutput"])
                else:
                    self.assertEqual(result.stdout, "")

    def test_feature_operations_are_not_approval_gated(self):
        for command in ["git commit -m 'ordinary'", "git push origin HEAD:feature/test",
                        "git push --force-with-lease origin HEAD:feature/test", "git push --force origin feature/test",
                        "git merge main", "git rebase main", "git reset --hard main",
                        "git status", "git diff", "git fetch origin", "gh pr merge 123 --squash"]:
            self.check_command(command, False)

    def test_main_writes_are_blocked(self):
        self.git("switch", "main")
        for command in ["git commit -m x", "git commit --amend -m x", "git merge feature/test",
                        "git rebase feature/test", "git reset --hard HEAD~1", "git cherry-pick HEAD",
                        "git revert HEAD", "git am /tmp/message", "git pull", "git pull --rebase",
                        "git branch -m renamed", "git update-ref HEAD HEAD~1"]:
            self.check_command(command, True)
        for command in ["git merge --ff-only origin/main", "git pull --ff-only", "git fetch origin",
                        "git status", "git branch --list main"]:
            self.check_command(command, False)

    def test_main_ref_targets_are_blocked_from_features(self):
        for command in ["git push origin HEAD:main", "git push origin +HEAD:refs/heads/main",
                        "git push origin :main", "git push --delete origin main", "git push --repo=origin HEAD:main",
                        "git push --all origin", "git push --mirror origin", "git branch -f main HEAD",
                        "git branch -D main", "git update-ref refs/heads/main HEAD", "git update-ref --stdin",
                        "git checkout -B main HEAD", "git rebase feature/test main",
                        "git fetch origin HEAD:refs/heads/main"]:
            self.check_command(command, True)

    def test_configured_signing_cannot_be_bypassed(self):
        for command in ["git commit --no-gpg-sign -m x", "git commit --no-verify -m x",
                        "git -c commit.gpgsign=false commit -m x", "git -ccommit.gpgsign=0 commit -m x",
                        "git --config-env=commit.gpgsign=SIGN commit -m x",
                        "GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=commit.gpgsign GIT_CONFIG_VALUE_0=false git commit -m x",
                        "git -c include.path=/tmp/config commit -m x", "git commit -Sother -m x",
                        "git config commit.gpgsign false", "git config --unset user.signingkey"]:
            self.check_command(command, True)
        for command in ["git -c commit.gpgsign=true commit -m x", "git config --get commit.gpgsign",
                        "git commit -S -m x", "git -c color.ui=false commit -m x"]:
            self.check_command(command, False)

    def test_literal_messages_and_heredocs_are_data(self):
        for command in ["git commit -m 'git push origin main; --no-gpg-sign'",
                        "git commit -m ';'", "git commit -m '|'", "git commit -m '('", "git commit -m 'line one\nline two'",
                        "git commit -m '$literal `text`'", "git commit -F - <<'EOF'\ngit reset main\nEOF\n",
                        "git commit -m 'quoted \"message\"' && git status"]:
            self.check_command(command, False)

    def test_compound_multiline_and_directory_resolution(self):
        self.git("switch", "main")
        for command in ["git status\ngit commit -m x", "git status; git commit -m x",
                        "git status && git commit -m x", "git checkout feature/test; git commit -m x"]:
            self.check_command(command, True)
        self.check_command("git commit -m x", True, cwd=self.repo / "subdir")
        self.check_command("git commit -m x", True, cwd=self.root, tool_cwd=self.repo)
        self.check_command("cd 'repo with spaces' && git commit -m x", True, cwd=self.root)
        self.check_command("git -C 'repo with spaces' commit -m x", True, cwd=self.root)
        self.check_command("git --git-dir 'repo with spaces/.git' commit -m x", True, cwd=self.root)

    def test_worktree_targets_its_own_branch(self):
        worktree = self.root / "worktree"
        self.git("worktree", "add", str(worktree), "main")
        self.check_command("git commit -m x", True, cwd=worktree)
        self.check_command("git commit -m x", False)

    def test_unresolved_protected_commands_fail_closed(self):
        for command in ["git commit -m 'unterminated", "git -C '$TARGET' commit -m x",
                        "git -C /missing/path commit -m x", "git commit -m $(echo x)",
                        "sh -c 'git commit -m x'", "git push origin 'HEAD:*'",
                        "git commit -F - <<EOF\nx\nEOF", "git commit -F - <<'EOF'\nx",
                        "cd missing || git commit -m x"]:
            self.check_command(command, True)

    def test_default_push_resolves_upstream_and_remote_refspec(self):
        self.git("config", "branch.feature/test.merge", "refs/heads/main")
        self.check_command("git push", True)
        self.git("config", "push.default", "current")
        self.check_command("git push", False)
        self.git("config", "remote.origin.push", "HEAD:main")
        self.check_command("git push", True)

    def test_unsigned_repo_is_not_reconfigured(self):
        self.git("config", "--unset", "commit.gpgsign")
        self.check_command("git commit --no-gpg-sign -m x", False)
        self.assertEqual(self.git("config", "user.name"), "Fixture")

    def test_native_malformed_payload_denies_without_granting_permissions(self):
        for runtime in ("claude", "codex"):
            for raw in ['{', '[]', '{"tool_name":"Bash","cwd":"/tmp","tool_input":{}}']:
                result = subprocess.run(["python3", "-B", str(SCRIPTS / (runtime + "_git_guard.py"))],
                                        input=raw, env=self.env, capture_output=True, text=True, check=True)
                output = json.loads(result.stdout)["hookSpecificOutput"]
                self.assertEqual(output["permissionDecision"], "deny")
                self.assertEqual(set(output), {"hookEventName", "permissionDecision", "permissionDecisionReason"})

    def test_registrations_use_supported_native_contracts(self):
        import tomllib
        root = SCRIPTS.parents[1]
        claude = json.loads((root / ".claude/settings.json").read_text())
        codex = tomllib.loads((root / ".codex/config.toml").read_text())
        for runtime, settings in [("claude", claude), ("codex", codex)]:
            guards = [h for group in settings["hooks"]["PreToolUse"] if group["matcher"] == "Bash"
                      for h in group["hooks"] if runtime + "_git_guard.py" in h["command"]]
            self.assertEqual(len(guards), 1)
            self.assertEqual(guards[0]["type"], "command")
            self.assertNotIn("async", guards[0])

    def test_matching_pushes_never_escape_main_protection(self):
        for refspec in [":", "+:"]:
            self.check_command("git push origin " + refspec, True)

    def test_effective_push_configuration_and_remote_selection(self):
        for command in ["git -c remote.origin.push=HEAD:main push origin",
                        "git -c push.default=matching push origin",
                        "git -c remote.origin.mirror=true push origin HEAD:feature/test",
                        "git --config-env=remote.origin.push=REFSPEC push origin"]:
            self.check_command(command, True)
        self.git("config", "push.default", "current")
        self.git("config", "remote.backup.push", "HEAD:main")
        for key in ["remote.pushDefault", "branch.feature/test.pushRemote", "branch.feature/test.remote"]:
            self.git("config", key, "backup")
            self.check_command("git push", True)
            self.git("config", "--unset", key)
        self.check_command("git -c color.ui=false push origin HEAD:feature/test", False)

    def test_escaped_newlines_follow_shell_option_tokenization(self):
        for command in ["git commit \\\n--no-gpg-sign -m x",
                        "git commit --no-gpg-\\\nsign -m x",
                        "git -c commit.gpgsign=\\\nfalse commit -m x",
                        "git push origin HEAD:ma\\\nin"]:
            self.check_command(command, True)
        self.check_command("git commit \\\n-m 'literal message'", False)

    def test_conflicting_fast_forward_flags_are_blocked(self):
        self.git("switch", "main")
        for command in ["git merge --ff-only --ff feature/test -m merged",
                        "git merge --ff --ff-only feature/test", "git merge --ff-only --no-ff feature/test",
                        "git pull --ff-only --ff", "git pull --ff-only --no-ff"]:
            self.check_command(command, True)
        self.check_command("git merge --ff-only origin/main", False)

    def test_uncertain_branch_state_is_protected_for_refs_and_renames(self):
        for suffix in ["git update-ref HEAD HEAD~1", "git symbolic-ref HEAD refs/heads/feature/test",
                       "git branch -m renamed", "git branch -Mrenamed", "git branch --copy copied",
                       "git push origin HEAD:HEAD"]:
            self.check_command("git switch main && " + suffix, True)

    def test_indirect_signing_config_changes_are_blocked(self):
        for command in ["git config --remove-section commit", "git config --remove-section=commit",
                        "git config --rename-section commit old", "git config rename-section commit old",
                        "git config include.path /tmp/config", "git config --add include.path /tmp/config",
                        "git config set includeIf.gitdir:/tmp/.path /tmp/config",
                        "git config --global --remove-section gpg"]:
            self.check_command(command, True)
        self.check_command("git config --get include.path", False)

    def test_abbreviated_protected_options_fail_closed(self):
        for command in ["git commit --allow-empty --no-gpg-s -m unsigned",
                        "git commit --no-ver -m unsigned", "git rebase --no-gpg-s main",
                        "git cherry-pick --no-gpg-s HEAD", "git revert --no-gpg-s HEAD",
                        "git am --no-gpg-s /tmp/patch", "git config --remove-sect commit",
                        "git config --rename-sect commit old", "git push --mir origin",
                        "git update-ref --std", "git fetch --refm=refs/heads/main:refs/heads/main origin",
                        "git switch main && git branch --mov renamed"]:
            self.check_command(command, True)
        for command in ["git commit --allow-empty --no-edit", "git rebase --interactive main",
                        "git push --force-with-lease origin HEAD:feature/test"]:
            self.check_command(command, False)

    def test_configured_fetch_destinations_cannot_modify_main(self):
        for mapping in ["+refs/heads/main:refs/heads/main", "+refs/heads/*:refs/heads/*", "main:main"]:
            self.git("config", "remote.origin.fetch", mapping)
            self.check_command("git fetch origin", True)
            self.check_command("git fetch origin main", True)
            self.check_command("git fetch", True)
        self.git("config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*")
        for command in ["git fetch origin", "git fetch origin main", "git fetch", "git fetch --prune origin",
                        "git -c remote.origin.fetch=+refs/heads/main:refs/remotes/origin/main fetch origin"]:
            self.check_command(command, False)
        self.check_command("git -c remote.origin.fetch=+refs/heads/main:refs/heads/main fetch origin", True)
        self.env["FETCH_MAPPING"] = "+refs/heads/main:refs/heads/main"
        self.check_command("git --config-env=remote.origin.fetch=FETCH_MAPPING fetch origin", True)
        self.git("config", "remote.backup.fetch", "+refs/heads/main:refs/heads/main")
        self.git("config", "branch.feature/test.remote", "backup")
        self.check_command("git fetch", True)
        self.check_command("git -c branch.feature/test.remote=backup fetch", True)

    def test_pull_fast_forward_also_checks_fetch_mapping(self):
        self.git("switch", "main")
        self.git("config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*")
        self.check_command("git pull --ff-only origin main", False)
        self.git("config", "remote.origin.fetch", "+refs/heads/main:refs/heads/main")
        self.check_command("git pull --ff-only origin main", True)
        for command in ["git fetch --all", "git fetch --multiple origin backup", "git fetch --refmap= origin"]:
            self.check_command(command, True)

    def test_short_option_operands_do_not_become_remote_names(self):
        self.git("config", "remote.origin.fetch", "+refs/heads/main:refs/heads/main")
        fetch_commands = ["git fetch -j 1 origin", "git fetch -j1 origin", "git fetch -qvj 1 origin",
                          "git fetch --jobs 1 origin", "git fetch --jobs=1 origin",
                          "git fetch -o option origin", "git fetch -ooption origin",
                          "git fetch -j1 -- origin", "git fetch -- origin"]
        for command in fetch_commands:
            self.check_command(command, True)
        self.git("config", "remote.origin.fetch", "+refs/heads/*:refs/remotes/origin/*")
        for command in fetch_commands:
            self.check_command(command, False)
        for command in ["git push -o option origin HEAD:main", "git push -ooption origin HEAD:main",
                        "git push -- origin HEAD:main", "git push -vooption origin HEAD:main"]:
            self.check_command(command, True)
        self.check_command("git push -o option origin HEAD:feature/test", False)

    def test_unknown_or_missing_short_operands_fail_closed(self):
        for command in ["git fetch -j", "git push -o", "git commit -m", "git switch -c",
                        "git update-ref -m", "git fetch -Z origin", "git pull -Z origin",
                        "git commit -Z -m x", "git branch -Z", "git rebase -Z main",
                        "git reset -Z HEAD", "git update-ref -Z HEAD HEAD~1", "git config -Z"]:
            self.check_command(command, True)
        for command in ["git commit -am 'safe message'", "git commit -m'safe message'",
                        "git commit -qm 'safe message'", "git commit -m '--no-gpg-sign'"]:
            self.check_command(command, False)

    def test_pull_preserves_configured_signing_when_it_can_make_commits(self):
        for command in ["git pull --no-rebase --no-gpg-sign origin incoming",
                        "git pull --no-rebase --no-gpg-s origin incoming",
                        "git pull -Sother origin incoming", "git pull --gpg-sign=other origin incoming",
                        "git -c commit.gpgsign=false pull --no-rebase origin incoming",
                        "git pull --no-verify origin incoming"]:
            self.check_command(command, True)
        for command in ["git pull --no-rebase origin incoming", "git pull -S origin incoming",
                        "git pull -j1 --no-rebase origin incoming"]:
            self.check_command(command, False)

    def test_main_fast_forward_exception_requires_verified_remote_main_sync(self):
        # Feature operations stay unaffected by main's sync-only exception.
        self.check_command("git merge --ff-only feature/test", False)
        self.check_command("git pull --ff-only origin feature/test", False)
        self.git("switch", "main")
        for command in ["git merge --ff-only origin/main", "git merge --ff-only refs/remotes/origin/main",
                        "git merge --ff-only", "git pull --ff-only", "git pull --ff-only origin",
                        "git pull --ff-only origin main", "git pull --ff-only origin refs/heads/main"]:
            self.check_command(command, False)
        for command in ["git merge --ff-only feature/test", "git merge --ff-only main",
                        "git merge --ff-only origin/feature/test", "git merge --ff-only origin/main feature/test",
                        "git pull --ff-only origin feature/test", "git pull --ff-only origin main feature/test",
                        "git -c branch.main.merge=refs/heads/feature/test pull --ff-only",
                        "git -c branch.main.remote=backup merge --ff-only origin/main"]:
            self.check_command(command, True)
        self.git("config", "--unset", "branch.main.merge")
        self.check_command("git pull --ff-only", True)
        self.check_command("git merge --ff-only origin/main", True)
        self.git("config", "branch.main.merge", "refs/heads/main")
        self.git("config", "--add", "branch.main.merge", "refs/heads/feature/test")
        self.check_command("git pull --ff-only", True)
        self.git("config", "--unset-all", "branch.main.merge")
        self.git("config", "branch.main.merge", "refs/heads/main")
        self.git("branch", "origin/main", "feature/test")
        self.check_command("git merge --ff-only origin/main", True)
        self.check_command("git merge --ff-only refs/remotes/origin/main", False)
        self.git("branch", "-d", "origin/main")
        self.git("config", "remote.origin.fetch", "+refs/heads/feature/test:refs/remotes/origin/main")
        self.check_command("git merge --ff-only origin/main", True)
        self.check_command("git pull --ff-only", True)

    def test_config_environment_cannot_change_targets_with_or_without_signing(self):
        for signed in (True, False):
            with self.subTest(signing=signed):
                self.git("config", "commit.gpgsign", "true" if signed else "false")
                self.git("switch", "main")
                for key, value in [("branch.main.remote", "backup"), ("branch.main.merge", "refs/heads/feature/test")]:
                    prefix = "GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=" + key + " GIT_CONFIG_VALUE_0=" + value
                    self.check_command(prefix + " git pull --ff-only", True)
                    self.check_command("env " + prefix + " git merge --ff-only origin/main", True)
                self.check_command("git pull --ff-only", False)
                self.check_command("git merge --ff-only origin/main", False)
                self.git("switch", "feature/test")
                for key, value, command in [
                    ("remote.origin.push", "HEAD:main", "git push origin"),
                    ("branch.feature/test.pushRemote", "backup", "git push"),
                    ("remote.origin.fetch", "+refs/heads/main:refs/heads/main", "git fetch origin"),
                    ("branch.feature/test.remote", "backup", "git fetch")]:
                    self.check_command("GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=" + key + " GIT_CONFIG_VALUE_0=" + value + " " + command, True)
                self.check_command("git fetch origin", False)
                self.check_command("git push origin HEAD:feature/test", False)


if __name__ == "__main__":
    unittest.main()
