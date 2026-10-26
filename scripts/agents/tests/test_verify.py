"""Verification command contracts using real Git and deterministic tool stand-ins."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


SOURCE = Path(__file__).resolve().parents[1] / "verify.py"


class VerificationTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="verification fixtures ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "checkout ' with spaces;$ "
        self.root.mkdir()
        self.script = self.root / "scripts/agents/verify.py"
        self.script.parent.mkdir(parents=True)
        shutil.copyfile(SOURCE, self.script)
        self.log = Path(self.temp.name) / "calls.jsonl"
        self.bin = Path(self.temp.name) / "bin"
        self.bin.mkdir()
        (self.bin / "git").symlink_to(shutil.which("git"))
        self.env = dict(os.environ, PATH=str(self.bin), VERIFY_TEST_LOG=str(self.log))
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.write(".gitignore", "node_modules/\nobj/\n.agent-local/\nignored/\n")
        self.write("README.md", "Documentation\n")
        self.write("MediaSet.Api/Existing.cs", "// initial\n")
        self.write("MediaSet.Remix/app/existing.ts", "// initial\n")
        self.write("scripts/agents/tests/test_smoke.py", "import unittest\nclass Smoke(unittest.TestCase):\n def test_ok(self): self.assertTrue(True)\n")
        self.commit("initial")
        self.git("switch", "-c", "feature")
        self.tools()

    def write(self, name, content="changed\n", root=None):
        path = (root or self.root) / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)
        return path

    def git(self, *args):
        return subprocess.check_output(["git", "-C", str(self.root), *args], stderr=subprocess.PIPE)

    def commit(self, message):
        self.git("add", ".")
        self.git("commit", "-m", message)

    def tools(self, root=None):
        root = root or self.root
        for name in ("dotnet", "npm", "node"):
            path = self.bin / name
            path.write_text(f"#!{sys.executable}\n"
                            "import json,os,pathlib,sys\n"
                            "name=pathlib.Path(sys.argv[0]).name\n"
                            "with open(os.environ['VERIFY_TEST_LOG'],'a') as stream:\n"
                            " stream.write(json.dumps([name,sys.argv[1:],os.getcwd()])+'\\n')\n"
                            "print('complete success output')\n"
                            "print('full diagnostic on stderr',file=sys.stderr)\n"
                            "sys.exit(int(os.environ.get('VERIFY_TEST_EXIT','0')) if name == os.environ.get('VERIFY_FAIL_TOOL') else 0)\n")
            path.chmod(0o755)
        for tool in ("eslint", "prettier", "tsc", "remix", "vitest"):
            self.write("MediaSet.Remix/node_modules/.bin/" + tool, "installed\n", root).chmod(0o755)
        for project in ("MediaSet.Api", "MediaSet.Api.Tests"):
            self.write(project + "/obj/project.assets.json", "{}", root)

    def verify(self, *args, root=None, cwd=None, env=None):
        root = root or self.root
        return subprocess.run([sys.executable, "-B", str(root / "scripts/agents/verify.py"), *args],
                              cwd=cwd or root, env=env or self.env, capture_output=True, text=True)

    def calls(self):
        return [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []

    def summary(self, root=None):
        directory = (root or self.root) / ".agent-local/verification"
        return json.loads(sorted(directory.glob("run-*/summary.json"))[-1].read_text())

    def passed(self, root=None):
        return {item["check"] for item in self.summary(root)["checks"] if item["status"] == "passed"}

    def test_branch_pushed_staged_unstaged_untracked_union_and_subdirectory(self):
        self.write("MediaSet.Api/Committed.cs")
        self.commit("committed backend")
        self.git("update-ref", "refs/remotes/origin/feature", "HEAD")
        self.git("config", "branch.feature.remote", "origin")
        self.git("config", "branch.feature.merge", "refs/heads/feature")
        self.write("MediaSet.Remix/app/staged.ts")
        self.git("add", "MediaSet.Remix/app/staged.ts")
        self.write("MediaSet.Remix/app/existing.ts", "unstaged\n")
        self.write("scripts/agents/untracked.py")
        before = self.git("diff", "--binary"), self.git("diff", "--cached", "--binary"), self.git("rev-parse", "HEAD")
        result = self.verify(cwd=self.root / "MediaSet.Remix/app")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertEqual(9, len(self.passed()))
        self.assertEqual(before, (self.git("diff", "--binary"), self.git("diff", "--cached", "--binary"), self.git("rev-parse", "HEAD")))
        selection = next((self.root / ".agent-local/verification").glob("run-*/selection.log")).read_text()
        for name in ("Committed.cs", "staged.ts", "existing.ts", "untracked.py"):
            self.assertIn(name, selection)

    def test_backend_only_and_frontend_only_commands(self):
        self.write("MediaSet.Api/New.cs")
        self.assertEqual(0, self.verify().returncode)
        self.assertEqual([["build", "MediaSet.sln", "--no-restore"], ["test", "MediaSet.Api.Tests/MediaSet.Api.Tests.csproj", "--no-restore"]], [call[1] for call in self.calls()])
        (self.root / "MediaSet.Api/New.cs").unlink()
        self.log.unlink()
        self.write("MediaSet.Remix/app/--quoted ' ;$(touch injected)\nfile.ts")
        self.assertEqual(0, self.verify().returncode)
        self.assertEqual([["run", script] for script in ("lint", "lint:format", "typecheck", "build", "test")], [call[1] for call in self.calls()])
        self.assertTrue(all(call[2] == str(self.root / "MediaSet.Remix") for call in self.calls()))
        self.assertFalse((self.root / "injected").exists())

    def test_each_local_state_selects_independently_including_cancelled_staged_edit(self):
        for state in ("staged", "unstaged", "untracked"):
            with self.subTest(state=state):
                path = self.write("MediaSet.Remix/app/" + ("new.ts" if state == "untracked" else "existing.ts"))
                if state == "staged":
                    self.git("add", "MediaSet.Remix/app/existing.ts")
                    path.write_text("// initial\n")
                self.assertEqual(0, self.verify().returncode)
                self.assertIn("frontend-types", self.passed())
                self.git("reset", "--hard", "HEAD")
                if state == "untracked":
                    path.unlink()

    def test_explicit_base_excludes_committed_changes_but_not_local_changes(self):
        self.write("MediaSet.Api/Committed.cs")
        self.commit("backend")
        self.assertEqual(0, self.verify("--base", "HEAD").returncode)
        self.assertEqual({"whitespace"}, self.passed())
        self.write("MediaSet.Remix/app/local.ts")
        self.assertEqual(0, self.verify("--base", "HEAD").returncode)
        self.assertIn("frontend-types", self.passed())
        self.assertNotIn("backend-tests", self.passed())

    def test_origin_main_preferred_over_local_main(self):
        self.write("MediaSet.Api/Committed.cs")
        self.commit("backend")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")
        self.assertEqual(0, self.verify().returncode)
        self.assertEqual({"whitespace"}, self.passed())

    def test_missing_invalid_and_unrelated_base_fall_back_full_without_fetch(self):
        self.git("branch", "-D", "main")
        for args in ((), ("--base", "no-such-ref"), ("--base=--quoted;$(touch injected)",)):
            with self.subTest(args=args):
                result = self.verify(*args)
                self.assertEqual(0, result.returncode, result.stdout + result.stderr)
                self.assertTrue(self.summary()["full"])
                self.assertEqual(9, len(self.passed()))
                self.assertIn("nothing was fetched", result.stdout)
        self.git("checkout", "--orphan", "unrelated")
        self.commit("orphan")
        self.git("switch", "feature")
        result = self.verify("--base", "unrelated")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertTrue(self.summary()["full"])

    def test_full_and_shared_configuration_select_all(self):
        self.assertEqual(0, self.verify("--full").returncode)
        self.assertEqual(9, len(self.passed()))
        self.write("Directory.Build.props", "<Project />\n")
        self.assertEqual(0, self.verify().returncode)
        self.assertEqual(9, len(self.passed()))

    def test_docs_no_changes_and_ignored_files_are_lightweight(self):
        for name in (None, "Development/new.md", "ignored/private.cs"):
            if name:
                self.write(name)
            result = self.verify()
            self.assertEqual(0, result.returncode)
            self.assertEqual({"whitespace"}, self.passed())
            self.assertEqual([], self.calls())
            self.assertEqual(8, sum(item["status"] == "skipped" for item in self.summary()["checks"]))

    def test_untracked_documentation_whitespace_is_checked_without_source_edits(self):
        document = self.write("Development/new ' ;$.md", "text with trailing whitespace \n")
        before = document.read_bytes()
        result = self.verify()
        self.assertNotEqual(0, result.returncode)
        check = self.summary()["checks"][0]
        self.assertEqual("failed", check["status"])
        self.assertIn("trailing whitespace", Path(check["log"]).read_text())
        self.assertEqual(before, document.read_bytes())
        self.assertEqual([], self.calls())

    def test_missing_git_falls_back_and_reports_failed_check_with_log(self):
        (self.bin / "git").unlink()
        result = self.verify()
        self.assertNotEqual(0, result.returncode)
        self.assertIn("nothing was fetched", result.stdout)
        self.assertTrue(self.summary()["full"])
        check = self.summary()["checks"][0]
        self.assertEqual("failed", check["status"])
        self.assertIn("git", Path(check["log"]).read_text())

    def test_failure_status_success_output_full_logs_and_remaining_checks(self):
        result = self.verify("--full", env=dict(self.env, VERIFY_FAIL_TOOL="dotnet", VERIFY_TEST_EXIT="7"))
        self.assertEqual(7, result.returncode)
        self.assertEqual(7, self.summary()["exit_code"])
        self.assertEqual(7, len(self.calls()))
        self.assertIn("PASSED: frontend-tests", result.stdout)
        self.assertIn("FAILED: backend-tests (exit 7)", result.stdout)
        for check in self.summary()["checks"]:
            self.assertIn(check["log"], result.stdout)
            self.assertTrue(Path(check["log"]).exists())
        log = Path(next(c for c in self.summary()["checks"] if c["check"] == "backend-tests")["log"]).read_text()
        self.assertIn("complete success output", log)
        self.assertIn("full diagnostic on stderr", log)
        self.assertIn("exit status: 7", log)

    def test_missing_prerequisites_are_failed_incomplete_without_bootstrap(self):
        for name in ("dotnet", "node", "npm"):
            tool = self.bin / name
            backup = tool.with_suffix(".backup")
            tool.rename(backup)
            result = self.verify("--full")
            self.assertNotEqual(0, result.returncode)
            self.assertIn("FAILED/INCOMPLETE", result.stdout)
            failures = [Path(c["log"]).read_text() for c in self.summary()["checks"] if c["status"] == "failed"]
            self.assertTrue(any("Missing " + name in text for text in failures))
            backup.rename(tool)
        (self.root / "MediaSet.Remix/node_modules/.bin/tsc").unlink()
        self.assertNotEqual(0, self.verify("--full").returncode)
        self.assertTrue(any("Missing installed project tool tsc" in Path(c["log"]).read_text() for c in self.summary()["checks"]))
        (self.root / "MediaSet.Api/obj/project.assets.json").unlink()
        self.assertNotEqual(0, self.verify("--full").returncode)
        self.assertTrue(all("restore" not in call[1] and "ci" not in call[1] for call in self.calls()))

    def test_retention_only_deletes_old_owned_runs_and_preserves_foreign_entries(self):
        directory = self.root / ".agent-local/verification"
        foreign = self.write(".agent-local/verification/unrelated/note.txt", "keep")
        external = Path(self.temp.name) / "external"
        external.mkdir()
        (external / "private.log").write_text("keep")
        (directory / ("run-20000101T000000000000Z-" + "0" * 32)).symlink_to(external, target_is_directory=True)
        for _ in range(12):
            self.assertEqual(0, self.verify().returncode)
        runs = [path for path in directory.glob("run-*") if not path.is_symlink()]
        self.assertEqual(10, len(runs))
        self.assertEqual("keep", foreign.read_text())
        self.assertEqual("keep", (external / "private.log").read_text())
        # An owned-looking directory with unexpected user data must also survive.
        oldest = min(runs)
        (oldest / "personal.txt").write_text("keep")
        self.assertEqual(0, self.verify().returncode)
        self.assertEqual("keep", (oldest / "personal.txt").read_text())
        self.assertEqual(b".agent-local/verification\n", self.git("check-ignore", ".agent-local/verification"))

    def test_log_directory_links_and_linked_dependencies_are_refused(self):
        external = Path(self.temp.name) / "external"
        external.mkdir()
        (self.root / ".agent-local").symlink_to(external, target_is_directory=True)
        self.assertNotEqual(0, self.verify("--full").returncode)
        self.assertEqual([], list(external.iterdir()))
        (self.root / ".agent-local").unlink()
        modules = self.root / "MediaSet.Remix/node_modules"
        modules.rename(external / "modules")
        modules.symlink_to(external / "modules", target_is_directory=True)
        self.assertNotEqual(0, self.verify("--full").returncode)
        self.assertTrue(any("linked node_modules" in Path(c["log"]).read_text() for c in self.summary()["checks"]))

    def test_installed_tool_link_outside_dependencies_is_refused(self):
        tool = self.root / "MediaSet.Remix/node_modules/.bin/tsc"
        target = Path(self.temp.name) / "external-tool"
        tool.rename(target)
        tool.symlink_to(target)
        result = self.verify("--full")
        self.assertNotEqual(0, result.returncode)
        check = next(c for c in self.summary()["checks"] if c["check"] == "frontend-types")
        self.assertEqual("failed", check["status"])
        self.assertIn("linked outside", Path(check["log"]).read_text())

    def test_worktree_logs_and_dependencies_are_independent(self):
        worktree = Path(self.temp.name) / "another worktree"
        self.git("worktree", "add", "-b", "second", str(worktree), "main")
        self.assertNotEqual(0, self.verify("--full", root=worktree).returncode)
        self.assertFalse((self.root / ".agent-local").exists())
        self.tools(worktree)
        result = self.verify("--full", root=worktree, cwd=self.root / "MediaSet.Api")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertTrue(all(str(worktree) in c[2] for c in self.calls()))
        self.assertTrue(all(str(worktree) in c["log"] for c in self.summary(worktree)["checks"]))

    def test_deleted_and_renamed_paths_keep_both_scopes(self):
        self.git("mv", "MediaSet.Api/Existing.cs", "MediaSet.Remix/app/renamed.ts")
        result = self.verify()
        self.assertEqual(0, result.returncode)
        self.assertIn("backend-tests", self.passed())
        self.assertIn("frontend-tests", self.passed())


class RegistrationsTests(unittest.TestCase):
    def test_no_test_rewriters_or_stop_hooks_and_guards_preserved(self):
        root = SOURCE.parents[2]
        claude = json.loads((root / ".claude/settings.json").read_text())
        self.assertNotIn("Stop", claude["hooks"])
        commands = [hook["command"] for entries in claude["hooks"].values() for entry in entries for hook in entry["hooks"]]
        self.assertTrue(any("claude_git_guard" in command for command in commands))
        self.assertTrue(any("claude_env_guard" in command for command in commands))
        for config in (root / ".claude/settings.json", root / ".codex/config.toml", root / ".codex/hooks.json"):
            if config.exists():
                content = config.read_text()
                self.assertFalse(any(name in content for name in ("filter-test-output", "verify-tests")),
                                 f"Obsolete test hook registered in {config.name}")
        for script in ("filter-test-output.sh", "verify-tests.sh"):
            self.assertFalse((root / ".claude/hooks" / script).exists())


if __name__ == "__main__":
    unittest.main()
