#!/usr/bin/env python3
"""Linux native-edit safeguard. Never log payloads or read environment contents."""

import argparse
import fcntl
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
from contextlib import contextmanager

MAX_INPUT = 2 * 1024 * 1024
MAX_FILES = 256
OPERATIONS = ("create", "update", "delete", "move-from", "move-to")
DENIAL = ("Environment edit blocked. See Development/AGENT_ENV_GUARD.md for "
          "classification and a temporary file/operation exception after explicit user authorization.")


class InvalidEdit(Exception):
    pass


def path_at(value, cwd):
    if not isinstance(value, str) or not value or len(value) > 4096 or any(
        ord(c) < 32 for c in value
    ):
        raise InvalidEdit()
    path = Path(value)
    return path if path.is_absolute() else cwd / path


def protected(path):
    def actual(name):
        return (name == ".env" or name.startswith(".env.")) and not name.endswith(
            (".example", ".sample", ".template")
        )
    # Both spellings matter: a safe-looking symlink must not hide a real env file.
    return actual(path.name) or actual(path.resolve().name)


def uses_symlink(path):
    # Compare with normalized absolute spelling so ./ and ../ remain supported.
    # This includes directory symlinks and dangling final symlinks.
    return path.is_symlink() or path.resolve() != Path(os.path.abspath(path))


def patch_targets(command, cwd):
    if not isinstance(command, str):
        raise InvalidEdit()
    lines = command.splitlines()
    if len(lines) < 3 or lines[0] != "*** Begin Patch" or lines[-1] != "*** End Patch":
        raise InvalidEdit()
    targets = []
    existence = {}

    def exists(path):
        key = str(path.resolve())
        return existence.get(key, path.exists())

    def mark(path, present):
        existence[str(path.resolve())] = present

    i = 1
    while i < len(lines) - 1:
        line = lines[i]
        kind = next((k for k in ("Add", "Update", "Delete")
                     if line.startswith(f"*** {k} File: ")), None)
        if kind is None:
            raise InvalidEdit()
        path = path_at(line[len(f"*** {kind} File: "):], cwd)
        i += 1
        # Native Add File overwrites existing destinations, like Claude Write.
        # Track earlier operations too: a second Add in this patch is an update.
        operation = {"Add": "update" if exists(path) else "create",
                     "Update": "update", "Delete": "delete"}[kind]
        if kind == "Update" and i < len(lines) - 1 and lines[i].startswith("*** Move to: "):
            destination = path_at(lines[i][13:], cwd)
            targets.append((destination, "move-to"))
            mark(destination, True)
            operation = "move-from"
            i += 1
        targets.append((path, operation))
        mark(path, operation not in ("delete", "move-from"))
        body = []
        while i < len(lines) - 1 and not lines[i].startswith(
            ("*** Add File: ", "*** Update File: ", "*** Delete File: ")
        ):
            body.append(lines[i])
            i += 1
        if kind == "Delete" and body:
            raise InvalidEdit()
        if kind == "Add" and any(not x.startswith("+") for x in body):
            raise InvalidEdit()
        if kind == "Update" and not valid_update(body):
            raise InvalidEdit()
        if len(targets) > MAX_FILES:
            raise InvalidEdit()
    return targets


def valid_update(body):
    # Native updates may omit @@ for their first hunk; later hunks use @@ or
    # @@ <skip-ahead context>. Empty lines are native empty context lines.
    hunk_lines = 0
    for index, line in enumerate(body):
        if line == "@@" or line.startswith("@@ "):
            if index and not hunk_lines:
                return False
            hunk_lines = 0
        elif line == "*** End of File":
            if index != len(body) - 1 or not hunk_lines:
                return False
        elif not line or line.startswith(("+", "-", " ")):
            hunk_lines += 1
        else:
            return False
    return hunk_lines > 0


def repo_at(cwd):
    result = subprocess.run(["git", "-C", str(cwd), "rev-parse", "--show-toplevel"],
                            capture_output=True, text=True, timeout=5)
    if result.returncode:
        raise InvalidEdit()
    return Path(result.stdout.strip()).resolve()


@contextmanager
def state_lock(root, create=False):
    directory = root / ".agent-local" / "env-guard"
    for item in (root / ".agent-local", directory):
        if item.is_symlink():
            raise InvalidEdit()
        if create:
            item.mkdir(mode=0o700, exist_ok=True)
    if not directory.exists():
        raise InvalidEdit()
    fd = os.open(directory / "lock", os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    with os.fdopen(fd, "r+") as lock:
        fcntl.flock(lock, fcntl.LOCK_EX)
        yield directory


def read_records(directory):
    path = directory / "exceptions.json"
    if path.is_symlink():
        raise InvalidEdit()
    if not path.exists():
        return []
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    with os.fdopen(fd) as stream:
        data = stream.read(65537)
    records = json.loads(data)
    if len(data) > 65536 or not isinstance(records, list) or len(records) > MAX_FILES:
        raise InvalidEdit()
    now = time.time()
    for record in records:
        if (not isinstance(record, dict) or set(record) != {"path", "operation", "expires"}
                or not isinstance(record["path"], str) or record["operation"] not in OPERATIONS
                or not isinstance(record["expires"], (float, int))
                or not math.isfinite(record["expires"])):
            raise InvalidEdit()
    return [r for r in records if now < r["expires"] <= now + 600]


def save_records(directory, records):
    fd, name = tempfile.mkstemp(prefix=".exceptions-", dir=directory)
    try:
        with os.fdopen(fd, "w") as stream:
            json.dump(records, stream)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(name, directory / "exceptions.json")
    finally:
        if os.path.exists(name):
            os.unlink(name)


def consume(root, targets):
    needed = {(str(p.resolve()), op) for p, op in targets if protected(p)}
    if not needed:
        return
    # A patch may unlink a symlink without deleting its referent, or replace an
    # alias before another write. Do not authorize protected changes using a
    # simulated filesystem that cannot represent those alias transitions.
    if any(uses_symlink(path) for path, _ in targets):
        raise InvalidEdit()
    if any(not Path(path).is_relative_to(root) for path, _ in needed):
        raise InvalidEdit()
    with state_lock(root) as directory:
        records = read_records(directory)
        available = {(r["path"], r["operation"]) for r in records}
        if not needed <= available:
            raise InvalidEdit()
        # Consume all grants atomically before allowing the native invocation.
        save_records(directory, [r for r in records if (r["path"], r["operation"]) not in needed])


def evaluate(payload, runtime):
    if not isinstance(payload, dict):
        raise InvalidEdit()
    tool = payload.get("tool_name")
    if tool not in (("Write", "Edit") if runtime == "claude" else ("apply_patch",)):
        return
    cwd_value = payload.get("cwd")
    if not isinstance(cwd_value, str) or not Path(cwd_value).is_absolute():
        raise InvalidEdit()
    cwd = Path(cwd_value)
    data = payload.get("tool_input")
    if not isinstance(data, dict):
        raise InvalidEdit()
    if runtime == "codex":
        targets = patch_targets(data.get("command"), cwd)
    else:
        path = path_at(data.get("file_path"), cwd)
        if tool == "Write":
            if not isinstance(data.get("content"), str):
                raise InvalidEdit()
            operation = "update" if path.exists() else "create"
        else:
            if not all(isinstance(data.get(k), str) for k in ("old_string", "new_string")):
                raise InvalidEdit()
            operation = "update"
        targets = [(path, operation)]
    if any(protected(p) for p, _ in targets):
        consume(repo_at(cwd), targets)


def run(runtime):
    try:
        raw = sys.stdin.buffer.read(MAX_INPUT + 1)
        if len(raw) > MAX_INPUT:
            raise InvalidEdit()
        evaluate(json.loads(raw), runtime)
    except (InvalidEdit, ValueError, OverflowError, OSError, RuntimeError, subprocess.SubprocessError):
        print(json.dumps({"hookSpecificOutput": {
            "hookEventName": "PreToolUse", "permissionDecision": "deny",
            "permissionDecisionReason": DENIAL}}))
    # Abstain on success: no allow decision that could broaden runtime permissions.


def grant():
    parser = argparse.ArgumentParser(description="Record a one-use env edit grant AFTER explicit user instruction.")
    parser.add_argument("--file", required=True)
    parser.add_argument("--operation", required=True, choices=OPERATIONS)
    parser.add_argument("--ttl", type=int, default=300)
    parser.add_argument("--authorized", action="store_true", required=True,
                        help="acknowledge explicit user authorization; this flag does not obtain it")
    args = parser.parse_args()
    try:
        if not 1 <= args.ttl <= 600:
            raise InvalidEdit()
        cwd = Path.cwd()
        root = repo_at(cwd)
        path = path_at(args.file, cwd)
        resolved = path.resolve()
        if not resolved.is_relative_to(root) or not protected(path) or uses_symlink(path):
            raise InvalidEdit()
        with state_lock(root, create=True) as directory:
            records = read_records(directory)
            records = [r for r in records if (r["path"], r["operation"]) != (str(resolved), args.operation)]
            if len(records) >= MAX_FILES:
                raise InvalidEdit()
            records.append({"path": str(resolved), "operation": args.operation,
                            "expires": time.time() + args.ttl})
            save_records(directory, records)
        print("One-use environment edit exception recorded; expires automatically.")
    except (InvalidEdit, ValueError, OverflowError, OSError, RuntimeError, subprocess.SubprocessError):
        print("Exception not recorded; check file, operation, TTL and checkout-local state.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(grant())
