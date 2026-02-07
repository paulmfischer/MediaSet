---
description: MediaSet project guidelines for code generation and modifications
applyTo: "**"
---

# GitHub Copilot Instructions for MediaSet

MediaSet is a full-stack application for managing personal media collections. The application consists of a .NET 10 backend API and a Remix.js frontend UI.

## Shared Guidelines (REQUIRED READING)

**All agents must follow the rules in:** `.github/instructions/shared.md`

This file contains:
- Project overview and tech stack
- Branch protection rules (NEVER commit to `main`)
- Commit conventions and signing requirements
- AI agent workflow (propose → approve → commit → push)
- Development and testing commands
- General code quality and testing practices

## Language-Specific Instructions (Consult before editing)

Agents must consult the project code-style and conventions documents before proposing or making changes:

- `.github/instructions/backend.instructions.md` — Backend guidelines (applies to `**/*.cs`)
- `.github/instructions/frontend.instructions.md` — Frontend guidelines (applies to `**/*.ts, **/*.tsx`)

These files contain file- and language-specific rules (naming, formatting, testing, patterns). Always follow them when proposing and implementing changes.