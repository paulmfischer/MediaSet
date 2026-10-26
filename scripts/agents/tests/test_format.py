"""External behavior of the deliberate formatting command, without real formatters."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


SOURCE = Path(__file__).resolve().parents[1] / "format.py"


class FormatTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="format behavior ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name) / "checkout with spaces"
        self.root.mkdir()
        self.script = self.root / "scripts/agents/format.py"
        self.script.parent.mkdir(parents=True)
        shutil.copyfile(SOURCE, self.script)
        self.frontend = self.root / "MediaSet.Remix"
        self.frontend.mkdir()
        self.log = Path(self.temp.name) / "calls.jsonl"
        self.env = dict(os.environ, FORMAT_TEST_LOG=str(self.log))
        self.git("init", "-b", "main")
        self.git("config", "user.name", "Fixture")
        self.git("config", "user.email", "fixture@example.invalid")
        self.git("config", "commit.gpgsign", "false")
        self.write(".gitignore", "node_modules/\nignored.ts\n")
        self.write("MediaSet.Remix/app/existing.ts")
        self.write("MediaSet.Remix/app/deleted.ts")
        self.commit("initial")
        self.git("switch", "-c", "feature")
        self.tools()

    def git(self, *args):
        return subprocess.check_output(["git", "-C", str(self.root), *args], stderr=subprocess.PIPE)

    def write(self, name, content="const value = 1;\n"):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content)
        return path

    def commit(self, message):
        self.git("add", ".")
        self.git("commit", "-m", message)

    def tools(self):
        for name in ("eslint", "prettier"):
            path = self.write("MediaSet.Remix/node_modules/.bin/" + name,
                f"#!{sys.executable}\n"
                "import json,os,pathlib,sys\n"
                "with open(os.environ['FORMAT_TEST_LOG'],'a') as stream:\n"
                " stream.write(json.dumps([pathlib.Path(sys.argv[0]).name,sys.argv[1:],os.getcwd()])+'\\n')\n"
                "print('visible formatter diagnostic')\n"
                "sys.exit(int(os.environ.get('FORMAT_TEST_EXIT','0')))\n")
            path.chmod(0o755)

    def run_format(self, *args, cwd=None, env=None, script=None):
        return subprocess.run([sys.executable, "-B", str(script or self.script), *args],
            cwd=cwd or self.root, env=env or self.env, capture_output=True, text=True)

    def calls(self):
        return [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []

    def test_default_includes_committed_pushed_staged_unstaged_untracked(self):
        self.write("MediaSet.Remix/app/committed.ts")
        self.commit("feature change")
        # A tracking feature ref at HEAD must not hide already-pushed changes.
        self.git("update-ref", "refs/remotes/origin/feature", "HEAD")
        self.git("config", "branch.feature.remote", "origin")
        self.git("config", "branch.feature.merge", "refs/heads/feature")
        self.write("MediaSet.Remix/app/staged.ts")
        self.git("add", "MediaSet.Remix/app/staged.ts")
        self.write("MediaSet.Remix/app/existing.ts", "changed\n")
        self.write("MediaSet.Remix/app/untracked.ts")
        self.write("MediaSet.Remix/app/data.json", "{}\n")
        self.write("other.ts")
        self.write("MediaSet.Remix/public/generated.ts")
        self.write("MediaSet.Remix/app/ignored.ts")
        (self.frontend / "app/deleted.ts").unlink()
        before = [self.git("rev-parse", "HEAD"), self.git("diff", "--cached", "--binary")]
        result = self.run_format(cwd=self.frontend / "app")
        self.assertEqual(0, result.returncode, result.stderr)
        prettier = {Path(call[1][-1]).name for call in self.calls() if call[0] == "prettier"}
        self.assertEqual({"committed.ts", "staged.ts", "existing.ts", "untracked.ts", "data.json"}, prettier)
        self.assertEqual(before, [self.git("rev-parse", "HEAD"), self.git("diff", "--cached", "--binary")])
        self.assertIn("Skipped deleted", result.stdout)
        self.assertTrue(all(call[2] == str(self.frontend) for call in self.calls()))

    def test_explicit_literal_paths_subdirectory_and_json(self):
        for name in ("- leading [space];$file.ts", "data.json"):
            self.write("MediaSet.Remix/app/" + name)
        self.write("MediaSet.Remix/app/unselected.ts")
        result = self.run_format("--", "- leading [space];$file.ts", "data.json", cwd=self.frontend / "app")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(["eslint", "prettier", "prettier"], [c[0] for c in self.calls()])
        self.assertTrue(all(c[1][-2] == "--" for c in self.calls()))

    def test_staged_change_cancelled_in_worktree_still_selected(self):
        self.write("MediaSet.Remix/app/existing.ts", "staged change\n")
        self.git("add", "MediaSet.Remix/app/existing.ts")
        self.write("MediaSet.Remix/app/existing.ts")
        result = self.run_format()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(2, len(self.calls()))

    def test_rename_and_newline_paths_are_literal(self):
        self.git("mv", "MediaSet.Remix/app/existing.ts", "MediaSet.Remix/app/renamed.ts")
        self.write("MediaSet.Remix/app/new\nline.ts")
        result = self.run_format()
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual({"renamed.ts", "new\nline.ts"},
            {Path(c[1][-1]).name for c in self.calls()})

    def test_json_needs_only_prettier_and_explicit_selection_excludes_user_changes(self):
        (self.frontend / "node_modules/.bin/eslint").unlink()
        selected = self.write("MediaSet.Remix/app/data.json", "{}")
        other = self.write("MediaSet.Remix/app/existing.ts", "user work\n")
        before = other.read_bytes()
        result = self.run_format(str(selected))
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(["prettier"], [c[0] for c in self.calls()])
        self.assertEqual(before, other.read_bytes())

    def test_help_and_conflicting_modes(self):
        result = self.run_format("--help")
        self.assertEqual(0, result.returncode)
        self.assertIn("From a subdirectory", result.stdout)
        for args in (("--all", "--base", "main"), ("--all", "file.ts"), ("--base", "main", "file.ts")):
            self.assertNotEqual(0, self.run_format(*args).returncode)
        self.assertEqual([], self.calls())

    def test_all_is_opt_in_and_override_uses_local_ref(self):
        result = self.run_format()
        self.assertEqual(0, result.returncode)
        self.assertEqual([], self.calls())
        result = self.run_format("--all")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(4, len(self.calls()))
        self.log.unlink()
        self.write("MediaSet.Remix/app/new.ts")
        self.commit("second")
        result = self.run_format("--base", "HEAD")
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual([], self.calls())

    def test_missing_invalid_base_fails_without_implicit_broadening(self):
        self.git("branch", "-D", "main")
        for args in ((), ("--base", "missing-ref"), ("--base", "--bad")):
            with self.subTest(args=args):
                self.assertNotEqual(0, self.run_format(*args).returncode)
        self.assertEqual([], self.calls())

    def test_scope_escapes_ignored_and_links_fail_before_any_tool(self):
        outside = Path(self.temp.name) / "outside.ts"
        outside.write_text("untouched")
        self.write("MediaSet.Remix/app/ignored.ts")
        link = self.frontend / "app/linked.ts"
        link.symlink_to(outside)
        directory = self.frontend / "linked"
        directory.symlink_to(outside.parent, target_is_directory=True)
        for name in (str(outside), "../outside.ts", "other.ts", "MediaSet.Remix/public/file.ts",
                     "MediaSet.Remix/app/ignored.ts", str(link), "MediaSet.Remix/linked/outside.ts"):
            with self.subTest(name=name):
                result = self.run_format("--", name)
                self.assertNotEqual(0, result.returncode, result.stdout)
        self.assertEqual([], self.calls())
        self.assertEqual("untouched", outside.read_text())

    def test_nonexistent_explicit_and_unsupported_default_are_noops(self):
        self.write("README.md", "docs\n")
        self.assertEqual(0, self.run_format("MediaSet.Remix/app/missing.ts").returncode)
        self.assertEqual(0, self.run_format().returncode)
        self.assertEqual([], self.calls())

    def test_missing_tools_and_node_fail_without_fetching(self):
        (self.frontend / "node_modules/.bin/prettier").unlink()
        result = self.run_format("--all")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Missing installed project tool: prettier", result.stderr)
        self.assertEqual([], self.calls())
        bin_dir = Path(self.temp.name) / "minimal-bin"
        bin_dir.mkdir()
        (bin_dir / "git").symlink_to(shutil.which("git"))
        result = self.run_format("--all", env=dict(self.env, PATH=str(bin_dir)))
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Node.js is required", result.stderr)

    def test_tool_failures_visible_and_nonzero(self):
        result = self.run_format("--all", env=dict(self.env, FORMAT_TEST_EXIT="7"))
        self.assertEqual(1, result.returncode)
        self.assertIn("visible formatter diagnostic", result.stdout)
        self.assertIn("FAILED: eslint (7)", result.stderr)
        self.assertIn("FAILED: prettier (7)", result.stderr)

    def test_linked_dependencies_rejected(self):
        modules = self.frontend / "node_modules"
        target = Path(self.temp.name) / "shared-modules"
        modules.rename(target)
        modules.symlink_to(target, target_is_directory=True)
        result = self.run_format("--all")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("linked node_modules", result.stderr)

    def test_tool_link_outside_dependencies_rejected(self):
        eslint = self.frontend / "node_modules/.bin/eslint"
        target = Path(self.temp.name) / "outside-eslint"
        eslint.rename(target)
        eslint.symlink_to(target)
        result = self.run_format("--all")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("tool linked outside", result.stderr)
        self.assertEqual([], self.calls())

    def test_worktree_owns_script_and_tools(self):
        worktree = Path(self.temp.name) / "separate worktree"
        self.git("worktree", "add", "-b", "second", str(worktree), "main")
        script = worktree / "scripts/agents/format.py"
        target = worktree / "MediaSet.Remix/app/worktree file.ts"
        target.write_text("change")
        # Missing independent dependencies must not reuse the primary checkout's tools.
        result = self.run_format(script=script, cwd=target.parent)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("Missing installed project tool", result.stderr)
        self.assertEqual([], self.calls())
        shutil.copytree(self.frontend / "node_modules", worktree / "MediaSet.Remix/node_modules")
        result = self.run_format(script=script, cwd=target.parent)
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertTrue(all(c[2] == str(worktree / "MediaSet.Remix") for c in self.calls()))


class RegistrationTests(unittest.TestCase):
    def test_supported_configurations_have_no_per_edit_formatting(self):
        root = SOURCE.parents[2]
        claude = json.loads((root / ".claude/settings.json").read_text())
        commands = [hook.get("command", "") for entries in claude.get("hooks", {}).values()
                    for entry in entries for hook in entry.get("hooks", [])]
        self.assertFalse(any("eslint-fix" in c or "prettier-format" in c for c in commands))
        self.assertTrue(any("claude_git_guard.py" in c for c in commands))
        self.assertTrue(any("claude_env_guard.py" in c for c in commands))
        codex = (root / ".codex/config.toml").read_text()
        self.assertNotIn("eslint-fix", codex)
        self.assertNotIn("prettier-format", codex)
        self.assertFalse((root / ".claude/hooks/eslint-fix.sh").exists())
        self.assertFalse((root / ".claude/hooks/prettier-format.sh").exists())


if __name__ == "__main__":
    unittest.main()
