#!/usr/bin/env python3
"""Bounded shell/Git guardrail. Never executes the proposed command.

This deliberately is not a shell interpreter or a security boundary. Unsupported
syntax around a recognizable protected operation requires splitting the call.
"""

import json
import os
from pathlib import Path
import re
import shlex
import subprocess
import sys


class Unresolved(Exception):
    pass


def git(cwd, args, env=None, missing_ok=False):
    result = subprocess.run(["git", *args], cwd=cwd, env=env, text=True,
                            capture_output=True, timeout=5, check=False)
    if missing_ok and result.returncode == 1:
        return ""
    if result.returncode:
        raise Unresolved()
    return result.stdout.strip()


PROTECTED = re.compile(r"\b(?:git|/[^\s;]+/git)\b", re.I)
SIGNING = {"commit.gpgsign", "user.signingkey", "gpg.format", "gpg.program", "gpg.openpgp.program", "gpg.ssh.program", "gpg.x509.program"}
WRITES = {"commit", "merge", "rebase", "reset", "cherry-pick", "revert", "am"}
COMMIT_PRODUCERS = WRITES | {"pull"}
# Git accepts abbreviated long options. Only full, reviewed spellings are
# supported for operations that may mutate protected state; never prefix-match.
LONG_OPTIONS = {
    "commit": {"all", "amend", "allow-empty", "allow-empty-message", "author", "date", "message", "file", "reuse-message", "reedit-message", "fixup", "squash", "reset-author", "signoff", "no-signoff", "gpg-sign", "no-gpg-sign", "no-verify", "verify", "edit", "no-edit", "only", "include", "interactive", "patch", "dry-run", "short", "branch", "porcelain", "long", "null", "verbose", "quiet", "cleanup", "status", "no-status", "trailer", "pathspec-from-file", "pathspec-file-nul"},
    "config": {"get", "get-all", "get-regexp", "get-urlmatch", "list", "show-origin", "show-scope", "null", "name-only", "type", "bool", "int", "bool-or-int", "path", "expiry-date", "default", "global", "system", "local", "worktree", "file", "blob", "includes", "no-includes", "add", "replace-all", "unset", "unset-all", "rename-section", "remove-section", "fixed-value", "value", "all", "append", "comment"},
    "merge": {"ff-only", "ff", "no-ff", "commit", "no-commit", "edit", "no-edit", "cleanup", "log", "no-log", "signoff", "no-signoff", "stat", "no-stat", "squash", "no-squash", "strategy", "strategy-option", "verify-signatures", "no-verify-signatures", "summary", "no-summary", "quiet", "verbose", "abort", "continue", "quit", "allow-unrelated-histories", "gpg-sign", "no-gpg-sign", "message", "file", "autostash", "no-autostash", "overwrite-ignore", "no-overwrite-ignore", "progress", "no-progress", "verify", "no-verify"},
    "pull": {"ff-only", "ff", "no-ff", "rebase", "no-rebase", "autostash", "no-autostash", "quiet", "verbose", "all", "append", "prune", "no-prune", "tags", "no-tags", "force", "depth", "unshallow", "update-shallow", "strategy", "strategy-option", "gpg-sign", "no-gpg-sign", "no-verify", "verify", "recurse-submodules", "no-recurse-submodules"},
    "fetch": {"all", "append", "atomic", "depth", "deepen", "shallow-since", "shallow-exclude", "unshallow", "update-shallow", "dry-run", "force", "keep", "multiple", "prune", "no-prune", "prune-tags", "no-prune-tags", "tags", "no-tags", "refmap", "recurse-submodules", "no-recurse-submodules", "jobs", "quiet", "verbose", "progress", "no-progress", "show-forced-updates", "no-show-forced-updates", "set-upstream", "update-head-ok", "no-write-fetch-head", "write-fetch-head", "ipv4", "ipv6", "server-option", "upload-pack", "negotiation-tip", "negotiate-only"},
}
LONG_OPTIONS.update({
    "rebase": {"onto", "keep-base", "no-keep-base", "continue", "skip", "abort", "quit", "edit-todo", "show-current-patch", "apply", "merge", "interactive", "rebase-merges", "no-rebase-merges", "root", "autosquash", "no-autosquash", "autostash", "no-autostash", "exec", "reschedule-failed-exec", "no-reschedule-failed-exec", "update-refs", "no-update-refs", "fork-point", "no-fork-point", "force-rebase", "no-ff", "committer-date-is-author-date", "reset-author-date", "ignore-date", "signoff", "gpg-sign", "no-gpg-sign", "empty", "keep-empty", "no-keep-empty", "strategy", "strategy-option", "quiet", "verbose", "stat", "no-stat"},
    "reset": {"soft", "mixed", "hard", "merge", "keep", "patch", "quiet", "refresh", "no-refresh", "recurse-submodules", "no-recurse-submodules", "pathspec-from-file", "pathspec-file-nul"},
    "cherry-pick": {"continue", "skip", "quit", "abort", "edit", "no-edit", "cleanup", "mainline", "no-commit", "signoff", "gpg-sign", "no-gpg-sign", "ff", "allow-empty", "allow-empty-message", "empty", "keep-redundant-commits", "strategy", "strategy-option", "rerere-autoupdate", "no-rerere-autoupdate"},
    "revert": {"continue", "skip", "quit", "abort", "edit", "no-edit", "cleanup", "mainline", "no-commit", "signoff", "gpg-sign", "no-gpg-sign", "strategy", "strategy-option", "rerere-autoupdate", "no-rerere-autoupdate", "reference"},
    "am": {"continue", "skip", "quit", "abort", "signoff", "keep", "keep-cr", "no-keep-cr", "scissors", "no-scissors", "patch-format", "reject", "3way", "no-3way", "ignore-space-change", "ignore-whitespace", "whitespace", "directory", "exclude", "include", "message-id", "no-message-id", "keep-non-patch", "empty", "committer-date-is-author-date", "ignore-date", "gpg-sign", "no-gpg-sign", "quiet", "interactive", "show-current-patch", "retry"},
    "branch": {"list", "show-current", "contains", "no-contains", "merged", "no-merged", "delete", "force", "move", "copy", "edit-description", "set-upstream-to", "unset-upstream", "track", "no-track", "recurse-submodules", "all", "remotes", "verbose", "quiet", "color", "no-color", "column", "no-column", "sort", "format", "points-at", "create-reflog", "omit-empty", "ignore-case"},
    "checkout": {"detach", "orphan", "force", "ours", "theirs", "merge", "conflict", "patch", "track", "no-track", "quiet", "progress", "no-progress", "recurse-submodules", "no-recurse-submodules", "ignore-other-worktrees", "overwrite-ignore", "no-overwrite-ignore", "pathspec-from-file", "pathspec-file-nul", "guess", "no-guess"},
    "switch": {"create", "force-create", "detach", "force", "discard-changes", "merge", "conflict", "track", "no-track", "guess", "no-guess", "orphan", "quiet", "progress", "no-progress", "recurse-submodules", "no-recurse-submodules", "ignore-other-worktrees"},
    "push": {"all", "branches", "mirror", "tags", "follow-tags", "delete", "prune", "force", "force-with-lease", "force-if-includes", "no-force", "no-force-with-lease", "no-force-if-includes", "repo", "receive-pack", "exec", "push-option", "set-upstream", "dry-run", "porcelain", "verbose", "quiet", "progress", "no-progress", "thin", "no-thin", "signed", "no-signed", "atomic", "no-atomic", "recurse-submodules", "no-recurse-submodules", "verify", "no-verify", "ipv4", "ipv6"},
    "update-ref": {"stdin", "no-deref", "create-reflog", "batch-updates"},
    "symbolic-ref": {"quiet", "short", "delete", "recurse", "no-recurse"},
})
LONG_OPTIONS["checkout"].update({"create", "force-create"})
LONG_OPTIONS["update-ref"].update({"message", "delete"})
LONG_OPTIONS["symbolic-ref"].add("message")
LONG_OPTIONS["pull"].add("jobs")

# A suffix ':' requires an operand; '?' permits only an attached optional
# operand. Canonical operands stay attached so they cannot become a remote/ref.
SHORT_OPTIONS = {
    "commit": {"a": "all", "m": "message:", "F": "file:", "C": "reuse-message:", "c": "reedit-message:", "S": "gpg-sign?", "n": "no-verify", "q": "quiet", "v": "verbose", "e": "edit", "s": "signoff", "o": "only", "i": "include", "p": "patch"},
    "merge": {"m": "message:", "F": "file:", "S": "gpg-sign?", "s": "strategy:", "X": "strategy-option:", "n": "no-stat", "q": "quiet", "v": "verbose", "e": "edit"},
    "fetch": {"j": "jobs:", "o": "server-option:", "u": "update-head-ok", "q": "quiet", "v": "verbose", "f": "force", "k": "keep", "p": "prune", "P": "prune-tags", "a": "append", "t": "tags", "n": "no-tags"},
    "pull": {"j": "jobs:", "r": "rebase?", "s": "strategy:", "X": "strategy-option:", "S": "gpg-sign?", "q": "quiet", "v": "verbose", "f": "force", "t": "tags", "p": "prune", "a": "append"},
    "push": {"o": "push-option:", "f": "force", "d": "delete", "u": "set-upstream", "v": "verbose", "q": "quiet", "n": "dry-run"},
    "branch": {"f": "force", "d": "delete", "D": "delete", "m": "move", "M": "move", "c": "copy", "C": "copy", "l": "create-reflog", "a": "all", "r": "remotes", "v": "verbose", "q": "quiet", "u": "set-upstream-to:"},
    "checkout": {"b": "create:", "B": "force-create:", "f": "force", "q": "quiet", "m": "merge", "p": "patch"},
    "switch": {"c": "create:", "C": "force-create:", "d": "detach", "f": "force", "q": "quiet", "m": "merge"},
    "rebase": {"i": "interactive", "m": "merge", "q": "quiet", "v": "verbose", "f": "force-rebase", "s": "strategy:", "X": "strategy-option:", "S": "gpg-sign?", "x": "exec:"},
    "reset": {"p": "patch", "q": "quiet"},
    "cherry-pick": {"n": "no-commit", "e": "edit", "s": "signoff", "m": "mainline:", "S": "gpg-sign?", "X": "strategy-option:"},
    "revert": {"n": "no-commit", "e": "edit", "s": "signoff", "m": "mainline:", "S": "gpg-sign?", "X": "strategy-option:"},
    "am": {"s": "signoff", "q": "quiet", "i": "interactive", "S": "gpg-sign?"},
    "config": {"l": "list", "f": "file:", "z": "null"},
    "update-ref": {"m": "message:", "d": "delete"},
    "symbolic-ref": {"m": "message:", "d": "delete", "q": "quiet"},
}
LONG_VALUES = {"author", "date", "message", "file", "reuse-message", "reedit-message", "fixup", "squash", "cleanup", "trailer", "pathspec-from-file", "onto", "exec", "empty", "strategy", "strategy-option", "mainline", "patch-format", "whitespace", "directory", "exclude", "include", "set-upstream-to", "sort", "format", "points-at", "orphan", "create", "force-create", "conflict", "repo", "receive-pack", "upload-pack", "push-option", "depth", "deepen", "shallow-since", "shallow-exclude", "jobs", "server-option", "negotiation-tip", "type", "default", "blob", "value", "comment"}


def options_for(op, arguments):
    """Parse only supported option forms; return canonical args and positionals."""
    canonical, positional = [], []
    i, ended = 0, False
    while i < len(arguments):
        argument = arguments[i]
        i += 1
        if argument == "--" and not ended:
            ended = True
            canonical.append(argument)
            continue
        if ended or argument == "-" or not argument.startswith("-"):
            positional.append(argument)
            canonical.append(argument)
            continue
        if argument.startswith("--"):
            name, equal, value = argument[2:].partition("=")
            if name not in LONG_OPTIONS.get(op, set()):
                raise Unresolved()
            # Section actions use their first positional operand, not '='.
            required = name in LONG_VALUES and not (name == "squash" and op == "merge" or name == "include" and op == "commit")
            if required and not equal:
                if i >= len(arguments):
                    raise Unresolved()
                value, i = arguments[i], i + 1
                equal = "="
            canonical.append("--" + name + ("=" + value if equal else ""))
            continue
        cluster = argument[1:]
        while cluster:
            char, cluster = cluster[0], cluster[1:]
            definition = SHORT_OPTIONS.get(op, {}).get(char)
            if definition is None:
                raise Unresolved()
            name = definition.rstrip(":?")
            value = None
            if definition.endswith((":", "?")):
                if cluster:
                    value, cluster = cluster, ""
                elif definition.endswith(":"):
                    if i >= len(arguments):
                        raise Unresolved()
                    value, i = arguments[i], i + 1
            canonical.append("--" + name + ("=" + value if value is not None else ""))
    return canonical, positional


def main_ref(ref):
    return ref in {"main", "refs/heads/main"}


def config_read_only(arguments, positionals):
    # options_for attaches option operands with '='. Inspect only options before
    # '--', so a value named get/list/--get cannot disguise a signing mutation.
    if positionals and positionals[0] in {"set", "unset", "rename-section", "remove-section", "edit"}:
        return False
    options = set()
    for argument in arguments:
        if argument == "--":
            break
        if argument.startswith("--"):
            options.add(argument.partition("=")[0])
    modifiers = {"--show-origin", "--show-scope", "--null", "--name-only",
                 "--type", "--bool", "--int", "--bool-or-int", "--path",
                 "--expiry-date", "--default", "--global", "--system",
                 "--local", "--worktree", "--file", "--blob", "--includes",
                 "--no-includes", "--fixed-value"}
    actions = {"--get", "--get-all", "--get-regexp", "--get-urlmatch", "--list"}
    if not options <= modifiers | actions | {"--all", "--value"}:
        return False
    if options & actions or positionals and positionals[0] in {"get", "list"}:
        return True
    # Legacy `git config [modifiers] <key>` reads only when no value or
    # explicit action is supplied. Modern set/unset and section verbs have
    # additional positional operands and cannot reach this case.
    return len(positionals) == 1 and options <= modifiers


def split_commands(command):
    # Literal, quoted heredocs are data, not additional commands. Their body may
    # contain text such as `git reset main`. Unquoted heredocs expand shell code.
    lines = command.splitlines(keepends=True)
    clean = []
    delimiter = None
    for line in lines:
        if delimiter is not None:
            if line.rstrip("\n") == delimiter:
                delimiter = None
            continue
        match = re.search(r"<<\s*(['\"])([A-Za-z_][A-Za-z_0-9]*)\1\s*$", line)
        if match:
            delimiter = match[2]
            line = line[:match.start()] + " < /dev/null\n"
        clean.append(line)
    if delimiter is not None:
        raise Unresolved()
    # shlex treats newlines as whitespace, losing command boundaries. Convert
    # only unquoted newlines to separators and reject executable expansions.
    source, quote, escaped = [], None, False
    for char in "".join(clean):
        if escaped:
            if char == "\n":
                # POSIX shells remove backslash-newline before tokenization,
                # including inside double quotes. Do not leave it in an option.
                source.pop()
                escaped = False
                continue
            source.append(char)
            escaped = False
            continue
        if char == "\\" and quote != "'":
            source.append(char)
            escaped = True
            continue
        if char in "'\"" and (quote is None or quote == char):
            quote = None if quote else char
        if char in "$`" and quote != "'":
            raise Unresolved()
        if quote and char in ";&|()<>\n":
            source.append(chr(0xE000 + ord(char)))
        else:
            source.append(";" if char == "\n" and quote is None else char)
    lexer = shlex.shlex("".join(source), posix=True, punctuation_chars=";&|()<>")
    lexer.whitespace = " \t\r"
    lexer.whitespace_split = True
    lexer.commenters = "#"
    parts, part = [], []
    for token in lexer:
        if token in {"|", "&", "(", ")", "<<", ";;"}:
            raise Unresolved()
        if re.fullmatch(r"[;&|()<>]+", token) and token not in {";", "&&", "||", "<", ">", ">>"}:
            raise Unresolved()
        if token in {";", "&&", "||", "\n"}:
            if part:
                parts.append(part)
                part = []
        else:
            part.append(token)
    if part:
        parts.append(part)
    return [["".join(chr(ord(c) - 0xE000) if 0xE000 <= ord(c) < 0xE100 else c for c in t) for t in p] for p in parts]


def evaluate(command, cwd):
    """Return a reason for denial, or None (no permission decision)."""
    if not PROTECTED.search(command):
        return None
    try:
        parts = split_commands(command)
        if "||" in command:
            raise Unresolved()
        branch_state = {}
        cwd = Path(cwd).resolve(strict=True)
        for words in parts:
            # Redirections with literal paths don't change Git's arguments.
            args = []
            i = 0
            while i < len(words):
                if words[i] in {"<", ">", ">>", "2>"}:
                    # shlex loses whether a preceding number was attached to a
                    # redirection (2>log) or a Git operand (2 >log). Reject that
                    # ambiguity rather than invent a remote/ref positional.
                    if args and args[-1].isdigit():
                        raise Unresolved()
                    if i + 1 >= len(words):
                        raise Unresolved()
                    i += 2
                    continue
                args.append(words[i])
                i += 1
            if not args:
                continue
            env = dict(os.environ)
            while args and re.match(r"^[A-Za-z_][A-Za-z_0-9]*=", args[0]):
                key, value = args.pop(0).split("=", 1)
                env[key] = value
            if args and args[0] == "env":
                args.pop(0)
                while args and re.match(r"^[A-Za-z_][A-Za-z_0-9]*=", args[0]):
                    key, value = args.pop(0).split("=", 1)
                    env[key] = value
            if not args:
                continue
            if args[0] == "cd":
                if len(args) != 2 or any(c in args[1] for c in "$`~*"):
                    raise Unresolved()
                cwd = (cwd / args[1]).resolve(strict=True)
                continue
            if Path(args[0]).name != "git":
                if any(PROTECTED.search(a) for a in args[1:]) or args[0] in {"export", "unset", "eval", "source", "."}:
                    raise Unresolved()
                continue
            args.pop(0)
            target = cwd
            options, overrides = [], []
            while args and args[0].startswith("-"):
                option = args.pop(0)
                if option in {"-C", "-c", "--git-dir", "--work-tree", "--config-env"}:
                    if not args:
                        raise Unresolved()
                    value = args.pop(0)
                elif option.startswith(("--git-dir=", "--work-tree=", "--config-env=")):
                    option, value = option.split("=", 1)
                elif option.startswith("-c") and len(option) > 2:
                    option, value = "-c", option[2:]
                elif option.startswith("-C") and len(option) > 2:
                    option, value = "-C", option[2:]
                elif option in {"--no-pager", "--no-optional-locks", "--literal-pathspecs"}:
                    continue
                else:
                    raise Unresolved()
                if any(c in value for c in "$`*?"):
                    raise Unresolved()
                if option == "-C":
                    target = (target / value).resolve(strict=True)
                elif option in {"-c", "--config-env"}:
                    overrides.append((option, value))
                else:
                    options.extend([option, value])
            if not args:
                continue
            op, rest = args[0], args[1:]
            if op not in WRITES | {"push", "branch", "update-ref", "symbolic-ref", "config", "pull", "checkout", "switch", "fetch"}:
                # Alias expansion can hide a protected operation. Resolve known
                # aliases only by asking the caller to use the underlying verb.
                try:
                    git(target, [*options, "config", "--get", "alias." + op], env)
                    raise Unresolved()
                except Unresolved:
                    if op not in {"status", "diff", "log", "show", "rev-parse", "fetch", "add", "restore"}:
                        raise Unresolved()
                continue
            rest, positionals = options_for(op, rest)
            # Every protection must inspect the same configuration Git will use.
            # These environment sources can relabel main's upstream or push/fetch
            # destinations even in repositories without configured signing.
            if any(k.startswith("GIT_CONFIG") for k in env if env.get(k) != os.environ.get(k)):
                return "Cannot evaluate Git config environment overrides; use the existing main, remote and signing settings."
            # Resolve the actual repo, using invocation env but without transient
            # config overrides when determining existing signing policy.
            repo = git(target, [*options, "rev-parse", "--absolute-git-dir"], env)
            try:
                branch = branch_state.get(repo, git(target, [*options, "symbolic-ref", "--short", "HEAD"], env))
            except Unresolved:
                branch = "HEAD"
            def config(key, effective=False, boolean=False):
                transient = [item for pair in overrides for item in pair] if effective else []
                conversion = ["--bool"] if boolean else []
                return git(target, ["--git-dir", repo, *transient, "config", *conversion, "--get", key],
                           env if effective else os.environ, missing_ok=True)
            def sync_source():
                """Only a single configured upstream of remote main is sync."""
                if branch != "main":
                    return False
                # Source authorization comes from existing config, not transient
                # options relabeling a feature branch as the main upstream.
                if any(value.partition("=")[0].lower().startswith(("branch.", "remote.", "include.", "includeif.")) for _, value in overrides):
                    return False
                def all_config(key):
                    return git(target, ["--git-dir", repo, "config", "--get-all", key], os.environ, missing_ok=True).splitlines()
                remotes, merges = all_config("branch.main.remote"), all_config("branch.main.merge")
                if len(remotes) != 1 or merges != ["refs/heads/main"] or remotes[0] == ".":
                    return False
                remote = remotes[0]
                mappings = all_config("remote." + remote + ".fetch")
                tracking = "refs/remotes/" + remote + "/main"
                allowed_maps = {"refs/heads/*:refs/remotes/" + remote + "/*", "refs/heads/main:" + tracking}
                if not mappings or any(mapping.lstrip("+") not in allowed_maps for mapping in mappings):
                    return False
                if op == "merge":
                    if positionals not in [[], [remote + "/main"], [tracking], ["@{upstream}"], ["@{u}"]]:
                        return False
                    # A merge source must already exist as the expected remote ref.
                    git(target, ["--git-dir", repo, "show-ref", "--verify", tracking], os.environ)
                    if positionals and git(target, ["--git-dir", repo, "rev-parse", "--symbolic-full-name", "--verify", positionals[0]], os.environ) != tracking:
                        return False
                    return True
                return positionals in [[], [remote], [remote, "main"], [remote, "refs/heads/main"]]
            signing = config("commit.gpgsign", boolean=True) == "true" or bool(config("user.signingkey"))
            policy_rest = []
            skip = False
            for a in rest:
                if skip:
                    skip = False
                    continue
                if op == "commit" and a in {"-m", "--message", "-F", "--file"}:
                    skip = True
                    continue
                if op == "commit" and a.startswith(("--message=", "--file=", "-m")):
                    continue
                policy_rest.append(a)
            if op in LONG_OPTIONS and any(a.startswith("--") and a != "--" and
                                          a[2:].split("=", 1)[0] not in LONG_OPTIONS[op] for a in policy_rest):
                return "Use full supported Git option names; abbreviated or unknown protected options cannot be evaluated safely."
            if signing:
                for option, value in overrides:
                    key, _, val = value.partition("=")
                    if key.lower().startswith(("include.", "includeif.")):
                        return "Cannot evaluate included config overrides; preserve configured signing."
                    if key.lower() in SIGNING:
                        if option == "--config-env" or key.lower() != "commit.gpgsign" or val.lower() not in {"true", "yes", "on", "1"}:
                            return "Preserve configured commit signing; remove signing overrides."
                if op in COMMIT_PRODUCERS and any(a.split("=", 1)[0] in {"--no-gpg-sign", "--no-verify"} or a.startswith("--gpg-sign=") for a in policy_rest):
                    return "Preserve configured signing; remove --no-gpg-sign/--no-verify and fix signing failures."
                if op == "config":
                    # Config section operations and include files can indirectly
                    # remove/replace signing without naming a signing key.
                    read_only = config_read_only(rest, positionals)
                    indirect = positionals[:1] == ["edit"] or any(a.split("=", 1)[0] in {"--remove-section", "--rename-section", "remove-section", "rename-section"} or
                                   a.lower().startswith(("include.", "includeif.")) for a in rest)
                    if not read_only and (indirect or any(a.lower().split("=", 1)[0] in SIGNING for a in rest)):
                        return "Do not remove or indirectly override configured signing; preserve signing sections and includes."
            if op in {"switch", "checkout"}:
                if (any(a in {"-B", "-C", "--force-create"} for a in rest) and "main" in rest or
                        any(a in {"-Bmain", "-Cmain", "--force-create=main", "--orphan=main"} for a in rest)):
                    return "Do not reset main's branch pointer."
                # The transition has not executed and can fail. Never infer a
                # feature HEAD for subsequent writes in this same tool call.
                branch_state[repo] = "UNKNOWN"
                continue
            if any(any(c in a for c in "$`*?") for a in policy_rest):
                raise Unresolved()
            if op in {"rebase", "pull"}:
                update_refs = config("rebase.updateRefs", effective=True, boolean=True) == "true"
                for a in rest:
                    if a in {"--update-refs", "--no-update-refs"}:
                        update_refs = a == "--update-refs"
                    elif a.startswith(("--update-refs=", "--no-update-refs=")):
                        raise Unresolved()
                if update_refs:
                    return ("Rebase can update main as a secondary ref; " +
                            ("use git -c rebase.updateRefs=false pull for a feature-only pull."
                             if op == "pull" else "use --no-update-refs for a feature-only rebase."))
            if op in WRITES and branch in {"main", "UNKNOWN"}:
                if op == "merge" and "--ff-only" in rest and not any(a.startswith(("--no-", "--squash")) or a == "--ff" for a in rest) and sync_source():
                    continue
                return "Cannot write main directly; use a feature branch and an approved PR. Synchronize with --ff-only."
            if op == "rebase" and any(main_ref(a) for a in rest):
                # main as upstream is normal, as a second positional branch isn't.
                positional = positionals
                if ("--root" in rest and positional and main_ref(positional[-1]) or
                        len(positional) > 1 and main_ref(positional[-1])):
                    return "Do not rebase main."
            if op == "pull" and branch in {"main", "UNKNOWN"} and ("--ff-only" not in rest or any(a.startswith(("--rebase", "--no-ff")) or a in {"--ff", "-r"} for a in rest)):
                return "Synchronize main using git pull --ff-only."
            if op == "pull" and branch in {"main", "UNKNOWN"} and not sync_source():
                return "Synchronize main only from its existing single remote-main upstream; feature integration requires an approved PR."
            if op == "branch" and any(main_ref(a) for a in rest):
                if not any(a in {"--list", "-l", "--show-current", "--contains", "--merged", "--no-merged"} for a in rest):
                    return "Do not change main's branch pointer."
            if op == "branch" and any(a.startswith(("-fmain", "-Dmain", "-dmain", "-Mmain", "-mmain")) for a in rest):
                return "Do not change main's branch pointer."
            if op == "branch" and branch in {"main", "UNKNOWN"} and any(a in {"--move", "--copy"} or a.startswith(("-m", "-M", "-c", "-C")) for a in rest):
                return "Do not rename or copy main through a branch-pointer operation."
            if op in {"update-ref", "symbolic-ref"}:
                if "--stdin" in rest or any(main_ref(a) or a == "HEAD" and branch in {"main", "UNKNOWN"} for a in rest):
                    return "Do not update main refs directly."
            if op == "push":
                if any(a in {"--all", "--branches", "--mirror"} for a in rest):
                    return "Push a specific feature branch; --all/--mirror can modify main."
                refs = list(positionals)
                remote_option = None
                for a in rest:
                    if a.startswith("--repo="):
                        remote_option = a.split("=", 1)[1]
                if remote_option is not None:
                    refs.insert(0, remote_option)
                remote = (refs[0] if refs else config("branch." + branch + ".pushRemote", True) or
                          config("remote.pushDefault", True) or config("branch." + branch + ".remote", True) or "origin")
                if config("remote." + remote + ".mirror", True).lower() in {"true", "yes", "on", "1"}:
                    return "Push a specific feature branch without remote mirror configuration."
                # First positional is the remote. Without explicit refs use Git's
                # push.default/upstream rules, which require an unambiguous target.
                refspecs = refs[1:]
                if not refspecs:
                    mode = config("push.default", True) or "simple"
                    if config("remote." + remote + ".push", True):
                        raise Unresolved()
                    upstream = config("branch." + branch + ".merge", True)
                    if branch in {"main", "UNKNOWN", "HEAD"} or mode not in {"simple", "current", "upstream", "nothing"} or mode in {"simple", "upstream"} and main_ref(upstream):
                        return "Push an explicit feature refspec; the default may target main."
                for ref in refspecs:
                    if ref.lstrip("+") == ":":
                        return "Matching pushes can update main; push an explicit feature refspec."
                    dest = ref.lstrip("+").split(":")[-1]
                    if main_ref(dest) or dest == "HEAD" and branch in {"main", "UNKNOWN"}:
                        return "Direct pushes to main are blocked; merge the approved PR through the hosting service."
            if op in {"fetch", "pull"}:
                # A configured fetch refspec can write local main even when the
                # command contains no destination, or names a source explicitly.
                # Refmap/multiple/all forms need a larger resolution model.
                if any(a.split("=", 1)[0] in {"--all", "--multiple", "--refmap", "--update-head-ok", "-u"} for a in rest):
                    raise Unresolved()
                positional = positionals
                remote = positional[0] if positional else config("branch." + branch + ".remote", True) or "origin"
                transient = [item for pair in overrides for item in pair]
                configured = git(target, ["--git-dir", repo, *transient, "config", "--get-all", "remote." + remote + ".fetch"], env, missing_ok=True)
                for ref in [*configured.splitlines(), *positional[1:]]:
                    if ":" not in ref:
                        continue
                    destination = ref.split(":")[-1]
                    if main_ref(destination) or ("*" in destination and not destination.startswith(("refs/remotes/", "refs/tags/"))):
                        return "Fetch destinations may modify main; fetch into remote-tracking refs and synchronize using --ff-only."
        return None
    except (Unresolved, OSError, ValueError, IndexError, subprocess.TimeoutExpired):
        return "Cannot safely resolve this Git operation. Split into literal Git commands with an explicit directory/ref; preserve main and signing."


def run(runtime):
    try:
        payload = json.load(sys.stdin)
        if payload.get("tool_name") != "Bash":
            return
        tool = payload.get("tool_input", {})
        command = tool.get("command")
        if not isinstance(command, str):
            raise ValueError()
        cwd = tool.get("cwd", payload.get("cwd"))
        reason = evaluate(command, cwd)
    except (ValueError, TypeError, AttributeError):
        reason = "Invalid Bash hook payload; cannot evaluate Git protection."
    if reason:
        print(json.dumps({"hookSpecificOutput": {"hookEventName": "PreToolUse", "permissionDecision": "deny", "permissionDecisionReason": reason}}))


if __name__ == "__main__":
    run(sys.argv[1] if len(sys.argv) > 1 else "")
