#!/usr/bin/env python3
"""Passive Linux checkout diagnosis and explicit, checkout-local dependency bootstrap."""

import argparse
from collections import Counter
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys


ROOT = Path(__file__).resolve().parents[2]
GUIDE = "Development/AGENT_SETUP.md"


def probe(command, cwd=ROOT):
    try:
        result = subprocess.run(command, cwd=cwd, text=True, capture_output=True,
                                timeout=30, check=False)
        return result.returncode, result.stdout.strip()
    except (OSError, subprocess.TimeoutExpired):
        return 1, ""


def prerequisites():
    """Never echo arbitrary tool output: it can contain credentials or machine details."""
    ready = True
    if platform.system() != "Linux":
        print("FAILED: Linux is the only supported platform.")
        ready = False
    if sys.version_info < (3, 11):
        print("FAILED: Python >=3.11 required; see " + GUIDE + "#prerequisites")
        ready = False
    checks = [
        ("git", ["git", "--version"], r"git version (\d+)\.(\d+)", (2, 0)),
        ("node", ["node", "--version"], r"v(\d+)\.(\d+)\.(\d+)", (24, 22, 1)),
        ("npm", ["npm", "--version"], r"(\d+)\.(\d+)", (1, 0)),
    ]
    for name, command, pattern, minimum in checks:
        status, output = probe(command)
        match = re.search(pattern, output)
        passed = status == 0 and match and tuple(map(int, match.groups())) >= minimum
        if name == "node" and match:
            passed = passed and int(match.group(1)) == 24
        print(("PASSED: " if passed else "FAILED: ") + name +
              (" available" if passed else "; see " + GUIDE + "#prerequisites"))
        ready = bool(passed) and ready
    # Ask the SDK resolver from this checkout: honors global.json rollForward.
    status, output = probe(["dotnet", "--version"])
    passed = status == 0 and bool(re.fullmatch(r"10\.\d+\.\d+", output))
    print(("PASSED: " if passed else "FAILED: ") +
          "global.json .NET SDK" + (" resolved" if passed else
          "; run dotnet --version here; see " + GUIDE + "#prerequisites"))
    ready = passed and ready
    status, output = probe(["git", "rev-parse", "--show-toplevel"])
    passed = status == 0 and Path(output).resolve() == ROOT
    print(("PASSED: " if passed else "FAILED: ") + "script belongs to this Git checkout")
    return passed and ready


def load_settings(path):
    if not path.exists():
        return None
    try:
        if path.suffix == ".toml":
            import tomllib
            with path.open("rb") as stream:
                data = tomllib.load(stream)
        else:
            data = json.loads(path.read_text())
        if not isinstance(data, dict):
            raise ValueError("settings must be an object")
        return data
    except (OSError, ValueError):
        print("WARNING: unreadable/invalid settings (contents suppressed); check " + GUIDE)
        return {}


def registrations(data):
    if not data or not isinstance(data.get("hooks"), dict):
        return []
    found = []
    for event, groups in data["hooks"].items():
        if not isinstance(groups, list):
            continue
        for group in groups:
            if not isinstance(group, dict) or not isinstance(group.get("hooks"), list):
                continue
            for hook in group["hooks"]:
                if isinstance(hook, dict) and isinstance(hook.get("command"), str):
                    found.append((event, str(group.get("matcher", "")), hook["command"]))
    return found


def hooks():
    # Inspect only known settings locations; never scan secret files or print values.
    codex_home = Path(os.environ.get("CODEX_HOME", str(Path.home() / ".codex")))
    claude_home = Path(os.environ.get("CLAUDE_CONFIG_DIR", str(Path.home() / ".claude")))
    sources = [
        ("Claude project", ROOT / ".claude/settings.json"),
        ("Claude project local", ROOT / ".claude/settings.local.json"),
        ("Claude user", claude_home / "settings.json"),
        ("Codex project JSON", ROOT / ".codex/hooks.json"),
        ("Codex project TOML", ROOT / ".codex/config.toml"),
        ("Codex user JSON", codex_home / "hooks.json"),
        ("Codex user TOML", codex_home / "config.toml"),
    ]
    seen = Counter()
    for label, path in sources:
        data = load_settings(path)
        entries = registrations(data)
        if data is None:
            print(label + ": not configured in this file; effective state unverified")
            continue
        # Include runtime in key: analogous hooks in different agents aren't duplicates.
        seen.update((label.split()[0], *entry) for entry in entries)
        disabled = data.get("disableAllHooks") is True if label.startswith("Claude") else (
            isinstance(data.get("features"), dict) and data["features"].get("hooks") is False)
        print(label + ": configured file; " + str(len(entries)) + " command registrations; " +
              ("disabled in this file; " if disabled else "") + "active/trusted unverified")
    if any(count > 1 for count in seen.values()):
        print("WARNING: duplicate event/matcher/command registrations detected; inspect /hooks.")
    print("Hook coverage: shared Git/environment guards; formatting and verification are explicit commands.")
    for runtime in ("Claude", "Codex"):
        commands = [entry[-1] for entry in seen if entry[0] == runtime]
        if any("block-main-commits" in c for c in commands) and any("git_guard.py" in c for c in commands):
            print("WARNING: " + runtime + " old/new Git guards compete; remove only the obsolete Git registration after review; see " + GUIDE)
        if any("protect-files" in c for c in commands) and any("env_guard.py" in c for c in commands):
            print("WARNING: " + runtime + " old/new environment guards compete; remove only the obsolete environment registration after review; see Development/AGENT_ENV_GUARD.md")
        retired = ("block-main-commits.sh", "protect-files.sh", "eslint-fix.sh",
                   "prettier-format.sh", "filter-test-output.sh", "verify-tests.sh")
        if any(name in command for command in commands for name in retired):
            print("WARNING: " + runtime + " obsolete project-style hook registrations detected; "
                  "review their source and archive only obsolete project entries; see " + GUIDE)
    print("Hook activation: unverified. Inspect native /hooks; plugins/managed/session layers")
    print("and semantically equivalent duplicates cannot be determined by this diagnostic.")
    if (ROOT / ".codex/hooks.json").exists():
        status, _ = probe(["git", "ls-files", "--error-unmatch", ".codex/hooks.json"])
        if status:
            print("WARNING: local untracked Codex candidate preserved; do not add or activate blindly.")
    for runtime in ("claude", "codex"):
        print(runtime + ": " + ("installed; live integration unverified" if shutil.which(runtime)
                                else "optional runtime not found"))
    for tool in ("bash", "jq"):
        print("Legacy hook prerequisite " + tool + ": " +
              ("available" if shutil.which(tool) else "missing; see " + GUIDE + "#prerequisites"))


def dependencies():
    required = [ROOT / "MediaSet.Remix/node_modules", ROOT / "MediaSet.Api/obj/project.assets.json",
                ROOT / "MediaSet.Api.Analyzers/obj/project.assets.json",
                ROOT / "MediaSet.Api.Tests/obj/project.assets.json"]
    present = not dependency_links()
    for path in required:
        if path.is_symlink():
            print("FAILED: shared/symlink dependency path: " + str(path.relative_to(ROOT)))
            present = False
        elif not path.exists():
            print("INCOMPLETE: missing " + str(path.relative_to(ROOT)))
            present = False
    print("Dependency freshness: unverified; rerun explicit bootstrap after manifest/lock changes.")
    return present


def dependency_links():
    """Reject linked roots before package managers could follow another checkout."""
    linked = []
    for project in ("MediaSet.Api", "MediaSet.Api.Analyzers", "MediaSet.Api.Tests", "MediaSet.Remix"):
        for child in ("", "obj", "bin", "node_modules"):
            path = ROOT / project / child
            if path.is_symlink():
                print("FAILED: dependency setup refuses symlink: " + str(path.relative_to(ROOT)))
                linked.append(path)
    local = ROOT / ".agent-local"
    for path in (local, local / "nuget", local / "npm-cache"):
        if path.is_symlink():
            print("FAILED: setup refuses linked local dependency/cache directory.")
            linked.append(path)
    return linked


def bootstrap():
    if dependency_links():
        return 1
    local = ROOT / ".agent-local"
    env = os.environ.copy()
    env.update({"NUGET_PACKAGES": str(local / "nuget"),
                "npm_config_cache": str(local / "npm-cache"),
                "DOTNET_CLI_TELEMETRY_OPTOUT": "1", "DOTNET_NOLOGO": "1"})
    steps = [(["dotnet", "restore", "MediaSet.sln"], ROOT),
             (["npm", "ci", "--no-audit", "--no-fund"], ROOT / "MediaSet.Remix")]
    for command, cwd in steps:
        print("Running: " + " ".join(command) + " (" + str(cwd.relative_to(ROOT)) + ")", flush=True)
        try:
            result = subprocess.run(command, cwd=cwd, env=env, check=False)
        except OSError:
            print("FAILED: dependency command could not start; see " + GUIDE)
            return 1
        if result.returncode:
            print("FAILED: " + command[0] + " dependency bootstrap; resolve diagnostics and rerun.")
            return result.returncode if result.returncode > 0 else 1
    print("PASSED: project dependencies restored for this checkout. Services were not started.")
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("command", choices=("diagnose", "bootstrap"))
    args = parser.parse_args()
    ready = prerequisites()
    if args.command == "bootstrap":
        return bootstrap() if ready else 1
    deps_ready = dependencies()
    hooks()
    print("Diagnosis " + ("complete" if ready and deps_ready else "incomplete") +
          "; no settings, trust, credentials, dependencies, or services changed.")
    if not deps_ready:
        print('Remediation: python3 "' + str(ROOT / "scripts/agents/setup.py") + '" bootstrap')
    return 0 if ready and deps_ready else 1


if __name__ == "__main__":
    sys.exit(main())
