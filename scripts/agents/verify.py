#!/usr/bin/env python3
"""Explicit completion checks with checkout-local diagnostics; never fetch or fix source."""

import argparse
from datetime import datetime, timezone
from functools import partial
import json
import os
from pathlib import Path
import re
import shlex
import shutil
import subprocess
import sys
import uuid


ROOT = Path(__file__).resolve().parents[2]
OWNER = "MediaSet verification logs v1\n"


def git(*args):
    result = subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True)
    if result.returncode:
        raise RuntimeError(result.stderr.decode(errors="replace").strip() or "Git command failed")
    return result.stdout


def names(*args):
    return {os.fsdecode(value) for value in git(*args).split(b"\0") if value}


def selection(args):
    try:
        base = args.base
        if base is None:
            for ref in ("refs/remotes/origin/main", "refs/heads/main"):
                if subprocess.run(["git", "-C", str(ROOT), "rev-parse", "--verify", ref],
                                  stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
                    base = ref
                    break
        if base is None:
            raise RuntimeError("No local origin/main or main comparison ref")
        commit = git("rev-parse", "--verify", "--end-of-options", base + "^{commit}").decode().strip()
        ancestor = git("merge-base", commit, "HEAD").decode().strip()
        changed = names("diff", "--name-only", "-z", "--no-renames", ancestor, "HEAD", "--")
        changed |= names("diff", "--name-only", "-z", "--no-renames", "--cached", "--")
        changed |= names("diff", "--name-only", "-z", "--no-renames", "--")
        changed |= names("ls-files", "--others", "--exclude-standard", "-z")
        return changed, ancestor, f"Branch comparison: {base!r}, merge base {ancestor}; {len(changed)} changed path(s)."
    except (RuntimeError, OSError) as error:
        return set(), None, f"Comparison unavailable: {error}. Running full checks; nothing was fetched."


def groups(changed, full):
    selected = {"backend", "frontend", "agents"} if full else set()
    for name in changed:
        path = Path(name)
        if path.suffix.lower() in {".md", ".txt", ".rst"} or path.parts[0:1] == ("docs",):
            continue
        if path.parts[0:1] in {("MediaSet.Api",), ("MediaSet.Api.Tests",)} or path.suffix in {".sln", ".slnx", ".csproj"} or name == "global.json":
            selected.add("backend")
        elif path.parts[0:1] == ("MediaSet.Remix",):
            selected.add("frontend")
        elif name.startswith(("scripts/agents/", ".claude/", ".codex/")):
            selected.add("agents")
        else:
            # Unknown/shared configuration must not silently miss application checks.
            selected.update(("backend", "frontend", "agents"))
    return selected


def untracked_documentation_check():
    """Git diff does not inspect new files, so check their text without staging."""
    paths = [ROOT / name for name in sorted(names("ls-files", "--others", "--exclude-standard", "-z"))
             if Path(name).suffix.lower() in {".md", ".txt", ".rst"}]
    errors = []
    for path in paths:
        if path.is_symlink() or not path.is_file():
            continue
        for number, line in enumerate(path.read_text().splitlines(), 1):
            if line != line.rstrip(" \t"):
                errors.append(f"{str(path.relative_to(ROOT))!r}:{number}: trailing whitespace")
    if errors:
        raise RuntimeError("\n".join(errors))


def safe_directory(path):
    for part in (ROOT / ".agent-local", ROOT / ".agent-local/verification"):
        if part.is_symlink():
            raise RuntimeError(f"Refusing linked log directory: {part}")
        part.mkdir(exist_ok=True)
    if not path.resolve().is_relative_to(ROOT):
        raise RuntimeError("Verification logs must belong to this checkout")


def cleanup(log_root):
    owned = []
    for path in log_root.iterdir():
        if not re.fullmatch(r"run-\d{8}T\d{12}Z-[0-9a-f]{32}", path.name) or path.is_symlink() or not path.is_dir():
            continue
        entries = list(path.iterdir())
        if any(entry.is_symlink() or not entry.is_file() or
               not (entry.name in {"owner", "summary.json", "selection.log"} or
                    re.fullmatch(r"check-[a-z-]+\.log", entry.name)) for entry in entries):
            continue
        marker = path / "owner"
        if marker.is_file() and marker.read_text() == OWNER:
            owned.append(path)
    for path in sorted(owned, key=lambda item: item.name)[:-10]:
        for entry in path.iterdir():
            entry.unlink()
        path.rmdir()


def frontend_prerequisites(tool):
    for name in ("node", "npm"):
        if not shutil.which(name):
            raise RuntimeError(f"Missing {name}; see Development/AGENT_SETUP.md")
    modules = ROOT / "MediaSet.Remix/node_modules"
    if modules.is_symlink():
        raise RuntimeError("Refusing linked node_modules; bootstrap this checkout independently")
    executable = modules / ".bin" / tool
    if not executable.is_file() or not os.access(executable, os.X_OK):
        raise RuntimeError(f"Missing installed project tool {tool}; run bash scripts/agents/setup.sh bootstrap explicitly")
    if not executable.resolve().is_relative_to(modules):
        raise RuntimeError(f"Refusing project tool linked outside this checkout's node_modules: {tool}")


def run_check(run, name, commands, cwd, enabled, prerequisite=None, show_output=False):
    log = run / f"check-{name}.log"
    code = 0
    status = "skipped" if not enabled else "passed"
    with log.open("w") as stream:
        if not enabled:
            stream.write("Not selected by this change set.\n")
        else:
            try:
                if prerequisite:
                    prerequisite()
                for command in commands:
                    stream.write(f"cwd: {cwd}\n$ {shlex.join(command)}\n")
                    stream.flush()
                    result = subprocess.run(command, cwd=cwd, stdout=stream, stderr=subprocess.STDOUT)
                    current = result.returncode if result.returncode >= 0 else 128 - result.returncode
                    stream.write(f"\nexit status: {current}\n")
                    if current and not code:
                        code = current
            except (RuntimeError, OSError, UnicodeError) as error:
                stream.write(f"Incomplete: {error}\n")
                code = 1
            if code:
                status = "failed"
    print(f"{status.upper()}: {name}" + (f" (exit {code})" if code else "") + f" — {log}", flush=True)
    if show_output and enabled:
        with log.open() as stream:
            shutil.copyfileobj(stream, sys.stdout)
        sys.stdout.flush()
    return {"check": name, "status": status, "exit_code": code, "log": str(log)}


def require_dotnet():
    if not shutil.which("dotnet"):
        raise RuntimeError("Missing dotnet; see Development/AGENT_SETUP.md")
    for project in ("MediaSet.Api", "MediaSet.Api.Tests"):
        if not (ROOT / project / "obj/project.assets.json").is_file():
            raise RuntimeError(f"Missing restored {project} dependencies; run bash scripts/agents/setup.sh bootstrap explicitly")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", help="local comparison ref (default origin/main, then main; never fetches)")
    parser.add_argument("--full", action="store_true", help="run all completion checks")
    parser.add_argument("--show-output", action="store_true", help="also print complete per-check logs (for CI diagnostics)")
    args = parser.parse_args()
    try:
        # Resolve this script's checkout, not the caller's checkout or common Git dir.
        if not (ROOT / ".git").exists():
            raise RuntimeError("Script must be inside its own Git checkout")
        if shutil.which("git") and Path(os.fsdecode(git("rev-parse", "--show-toplevel")).removesuffix("\n")).resolve() != ROOT:
            raise RuntimeError("Script is not in the root of its own Git checkout")
        log_root = ROOT / ".agent-local/verification"
        safe_directory(log_root)
        run = log_root / (datetime.now(timezone.utc).strftime("run-%Y%m%dT%H%M%S%fZ-") + uuid.uuid4().hex)
        run.mkdir()
        (run / "owner").write_text(OWNER)
        changed, ancestor, explanation = selection(args)
        full = args.full or ancestor is None
        if args.full:
            explanation = "Full checks explicitly requested. " + explanation
        selected = groups(changed, full)
        print(explanation, flush=True)
        print("Selected groups: " + (", ".join(sorted(selected)) or "documentation/no application changes"), flush=True)
        (run / "selection.log").write_text(explanation + "\n" + json.dumps(sorted(changed), ensure_ascii=True, indent=2) + "\n")
        whitespace = [["git", "diff", "--check", "--cached"], ["git", "diff", "--check"]]
        if ancestor:
            whitespace.insert(0, ["git", "diff", "--check", ancestor, "HEAD", "--"])
        check = partial(run_check, run, show_output=args.show_output)
        results = [check("whitespace", whitespace, ROOT, True, untracked_documentation_check)]
        results.append(check("agent-contracts", [[sys.executable, "-B", "-m", "unittest", "discover", "-s", "scripts/agents/tests", "-v"]], ROOT, "agents" in selected))
        results.append(check("backend-build", [["dotnet", "build", "MediaSet.sln", "--no-restore"]], ROOT, "backend" in selected, require_dotnet))
        results.append(check("backend-tests", [["dotnet", "test", "MediaSet.Api.Tests/MediaSet.Api.Tests.csproj", "--no-restore"]], ROOT, "backend" in selected, require_dotnet))
        for name, script, tool in (("lint", "lint", "eslint"), ("format", "lint:format", "prettier"),
                                   ("types", "typecheck", "tsc"), ("build", "build", "remix"), ("tests", "test", "vitest")):
            results.append(check("frontend-" + name, [["npm", "run", script]], ROOT / "MediaSet.Remix",
                                     "frontend" in selected, lambda tool=tool: frontend_prerequisites(tool)))
        code = next((result["exit_code"] for result in results if result["exit_code"]), 0)
        (run / "summary.json").write_text(json.dumps({"base": ancestor, "full": full, "checks": results, "exit_code": code}, indent=2) + "\n")
        cleanup(log_root)
        print(f"{'FAILED/INCOMPLETE' if code else 'PASSED'}: verification — {run / 'summary.json'}", flush=True)
        return code
    except (RuntimeError, OSError) as error:
        print(f"FAILED/INCOMPLETE: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
