#!/usr/bin/env python3
"""Deliberate checkout-local frontend fixes; never fetch, stage, or commit."""

import argparse
import os
from pathlib import Path
import shutil
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
FRONTEND = ROOT / "MediaSet.Remix"
CODE = {".js", ".jsx", ".ts", ".tsx"}
EXCLUDED = {"node_modules", "build", "coverage", ".cache", ".git", "public"}


def git(*args):
    result = subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode(errors="replace").strip())
    return result.stdout


def names(*args):
    return {os.fsdecode(name) for name in git(*args).split(b"\0") if name}


def relevant(name):
    path = Path(name)
    return (
        path.parts[0:1] == ("MediaSet.Remix",)
        and not EXCLUDED.intersection(path.parts[1:])
        and (path.suffix in CODE or (path.suffix == ".json" and path.parts[1:2] == ("app",)))
    )


def selected(args):
    available = names("ls-files", "-z", "--cached", "--others", "--exclude-standard")
    if args.files:
        candidates = set()
        for value in args.files:
            path = Path(os.path.abspath(value))
            try:
                name = str(path.relative_to(ROOT))
            except ValueError:
                raise RuntimeError(f"File is outside this checkout: {value!r}") from None
            if not relevant(name):
                raise RuntimeError(f"Unsupported project file: {value!r}")
            if path.exists() and name not in available:
                raise RuntimeError(f"Ignored file is outside formatting scope: {value!r}")
            candidates.add(name)
    elif args.all:
        candidates = available
    else:
        base = args.base
        if base is None:
            for ref in ("refs/remotes/origin/main", "refs/heads/main"):
                if subprocess.run(
                    ["git", "-C", str(ROOT), "rev-parse", "--verify", ref],
                    stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                ).returncode == 0:
                    base = ref
                    break
        if base is None:
            raise RuntimeError("No local main comparison base. Use --base REF, explicit files, or --all; nothing was fetched.")
        commit = git("rev-parse", "--verify", "--end-of-options", base + "^{commit}").decode().strip()
        ancestor = git("merge-base", commit, "HEAD").decode().strip()
        candidates = names("diff", "--name-only", "-z", "--no-renames", ancestor, "HEAD", "--")
        candidates |= names("diff", "--name-only", "-z", "--no-renames", "--cached", "--")
        candidates |= names("diff", "--name-only", "-z", "--no-renames", "--")
        candidates |= names("ls-files", "--others", "--exclude-standard", "-z")
        print(f"Branch comparison: {base} (merge base {ancestor[:12]})", flush=True)
    files = []
    for name in sorted(candidates):
        if not relevant(name):
            continue
        path = ROOT / name
        if any(parent.is_symlink() for parent in (path, *path.parents)):
            raise RuntimeError(f"Refusing linked formatting path: {name!r}")
        if not path.exists():
            print(f"Skipped deleted/nonexistent file: {name!r}", flush=True)
            continue
        if not path.is_file():
            raise RuntimeError(f"Not a regular file: {name!r}")
        files.append(path)
    return files


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", help="local Git comparison ref for branch changes (no fetch)")
    parser.add_argument("--all", action="store_true", help="format all supported, nonignored frontend files")
    parser.add_argument("files", nargs="*", help="explicit files relative to caller; use -- before names")
    parser.epilog = 'From a subdirectory: python3 -B "$(git rev-parse --show-toplevel)/scripts/agents/format.py" -- ./file.ts'
    args = parser.parse_args()
    if (args.all and (args.base or args.files)) or (args.base and args.files):
        parser.error("choose default/--base, explicit files, or --all")
    try:
        if not shutil.which("git"):
            raise RuntimeError("Git is required; see Development/AGENT_SETUP.md.")
        files = selected(args)
        if not files:
            print("No supported frontend files selected.")
            return 0
        if not shutil.which("node"):
            raise RuntimeError("Node.js is required; see Development/AGENT_SETUP.md.")
        tools = [FRONTEND / "node_modules/.bin" / name for name in ("eslint", "prettier")]
        if (FRONTEND / "node_modules").is_symlink():
            raise RuntimeError("Refusing linked node_modules; bootstrap this checkout independently.")
        required = tools if any(path.suffix in CODE for path in files) else tools[1:]
        for tool in required:
            if not tool.is_file() or not os.access(tool, os.X_OK):
                raise RuntimeError(f"Missing installed project tool: {tool.name}. Run bash scripts/agents/setup.sh bootstrap explicitly.")
            if not tool.resolve().is_relative_to(FRONTEND / "node_modules"):
                raise RuntimeError(f"Refusing project tool linked outside this checkout's node_modules: {tool.name}")
        print(f"Formatting {len(files)} file(s); review git diff before staging.", flush=True)
        failed = False
        for path in files:
            commands = []
            if path.suffix in CODE:
                commands.append([str(tools[0]), "--fix", "--", str(path)])
            commands.append([str(tools[1]), "--write", "--", str(path)])
            for command in commands:
                result = subprocess.run(command, cwd=FRONTEND)
                if result.returncode:
                    print(f"FAILED: {Path(command[0]).name} ({result.returncode}) for {str(path.relative_to(ROOT))!r}", file=sys.stderr, flush=True)
                    failed = True
        return 1 if failed else 0
    except (RuntimeError, OSError) as error:
        print(f"FAILED: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
