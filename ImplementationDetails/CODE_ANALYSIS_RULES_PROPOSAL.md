# Code Analysis Rules Proposal

**Issue:** #490 - Review and implement more dotnet code rules  
**Date:** February 8, 2026  
**Status:** Awaiting Approval

## Overview

This document outlines the proposed changes to enforce code quality and consistency across all .NET projects in the MediaSet solution through EditorConfig and code analysis rules.

## Research Summary

### Microsoft Code Analysis Categories

11 rule categories are available:
1. **Design** - Framework design guidelines adherence
2. **Documentation** - XML documentation for public APIs
3. **Globalization** - World-ready applications
4. **Interoperability** - COM/platform interop
5. **Maintainability** - Code maintenance support
6. **Naming** - .NET naming conventions
7. **Performance** - High-performance patterns
8. **Reliability** - Memory and thread usage
9. **Security** - Security flaw prevention
10. **Style** (IDE rules) - Code style consistency
11. **Usage** - Proper .NET API usage

### Current Codebase Patterns

**var Usage:**
- Heavy use of `var` throughout codebase (50+ instances)
- Used for both obvious types and complex types
- Current settings: mixed (false for built-in, true when apparent, false elsewhere)

**Exception Handling:**
- 20+ `catch (Exception)` blocks found
- Mix of logged exceptions and silent catches
- CA1031 currently disabled

**Async/ConfigureAwait:**
- Only 1 instance of `.ConfigureAwait(false)` in entire codebase
- CA2007 currently disabled
- ASP.NET Core apps don't require ConfigureAwait

**Null Checking:**
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Extensive use of null-coalescing (`??`) and null-conditional (`?.`) operators

### Existing Configuration

**Before this change:**
- Only `MediaSet.Api/.editorconfig` existed
- 114 total rules defined
- Almost all rules set to `suggestion` or `silent`
- Only IDE0005 (unused usings) was configured as error (but commented out)
- Several important CA rules explicitly disabled

**After Phase 2 & 3:**
- Created root `.editorconfig` at solution level
- Created root `.globalconfig` with architecture rules
- All three .NET projects now inherit common configuration
- Existing rules preserved at current severity levels

## Proposed Rule Changes

### Category A: Critical Rules → `error` (Build-Breaking)

| Rule | Current | Proposed | Rationale |
|------|---------|----------|-----------|
| **IDE0005** | commented `warning` | `error` | Remove unnecessary usings - Already resolved in codebase (PR #486), prevents future clutter |

**Estimated Impact:** ✅ **0 violations** (already cleaned up)

### Category B: Important Quality Rules → `error` (Build-Breaking)

| Rule | Current | Proposed | Rationale |
|------|---------|----------|-----------|
| **CA1031** | `none` | `error` | Do not catch general exceptions - Found 20+ violations; helps identify poor error handling patterns |
| **CA1716** | `none` | `error` | Identifiers shouldn't match keywords - Prevents naming confusion with language keywords |
| **IDE0008** | N/A | `error` | Use explicit type declaration - Discourages var for non-obvious types, improves code clarity |

**Estimated Impact:** 🛑 **70-80 violations (Build-Breaking)**
- CA1031: ~20 violations
- CA1716: ~0-5 violations  
- IDE0008: ~50+ violations

### Category C: Rules to Keep Disabled → `none`

| Rule | Current | Proposed | Rationale |
|------|---------|----------|-----------|
| **CA1062** | `none` | `none` | Validate public method arguments - Too noisy for ASP.NET Core with model binding/validation attributes |
| **CA1303** | `none` | `none` | Localization - Not currently implementing internationalization |
| **CA2007** | `none` | `none` | ConfigureAwait - ASP.NET Core doesn't require it (no SynchronizationContext in async operations) |
| **AD0001** | `none` | `none` | Analyzer exceptions - Technical noise from Roslyn analyzer infrastructure issues |

**Rationale:** These rules don't align with ASP.NET Core best practices or current project requirements.

### Category D: Style Rules → Upgrade to `error`

| Rule | Current | Proposed | Rationale |
|------|---------|----------|-----------|
| **Naming conventions** (4 rules) | `suggestion` | `error` | Enforce interface prefix (I), PascalCase for types, camelCase for fields - Standard .NET conventions |
| **dotnet_style_readonly_field** | `suggestion` | `error` | Mark fields readonly when possible - Improves immutability and thread safety |
| **dotnet_code_quality_unused_parameters** | `suggestion` | `error` | Remove unused parameters - Indicates dead code or oversight |

**Estimated Impact:** 🛑 **10-30 violations (Build-Breaking)**
- Naming conventions: ~0-10 violations
- Readonly fields: ~10-20 violations
- Unused parameters: ~0-5 violations

### Category E: Keep Current Severity (No Changes)

All other existing rules remain at current severity levels:
- **Formatting rules** - Keep as-is (IDE enforcement only)
- **Expression preferences** - Keep at `suggestion` or `silent`
- **Pattern matching preferences** - Keep at `suggestion`
- **Code style preferences** - Keep at `suggestion` or `silent`

## Total Estimated Impact

**Build Breaking (Errors):**
- 🛑 **80-110 total violations** across Categories B + D
- CA1031 (general exceptions): ~20 violations
- IDE0008 (var usage): ~50+ violations
- Naming conventions: ~0-10 violations
- Readonly fields: ~10-20 violations
- Unused parameters: ~0-5 violations

**Build Warnings:**
- None (all rules are errors or disabled)

## Implementation Options

### Option 1: Aggressive Enforcement (Recommended)
**Approach:** Apply all Category A + B + D rules immediately

**Pros:**
- Strictest enforcement from the start
- Single PR to fix all violations
- Clear baseline for future code

**Cons:**
- Requires fixing 80-110 violations upfront
- Larger initial time investment

**Timeline:** 1 PR (this one)

---

### Option 2: Conservative Phased Approach
**Approach:** Apply only Category A (errors) now, add warnings in phases

**Phase 1:** Enable IDE0005 (errors only)  
**Phase 2:** Add naming conventions (warnings)  
**Phase 3:** Add CA1031 and CA1716 (warnings)  
**Phase 4:** Add IDE0008 if desired (warnings)

**Pros:**
- Gradual adoption
- Smaller PRs, easier to review
- Can assess impact before proceeding

**Cons:**
- Multiple PRs required
- Longer timeline to reach full enforcement
- Code written between phases won't follow all rules

**Timeline:** 4 separate PRs

---

### Option 3: Moderate Balanced Approach
**Approach:** Apply Category A + high-value warnings (CA1031, naming), defer var enforcement

**Immediate:**
- IDE0005 (error)
- CA1031 general exceptions (error)
- CA1716 keyword naming (error)
- Naming conventions (error)
- Readonly fields (error)
- Unused parameters (error)

**Deferred:**
- IDE0008 var usage (considered too disruptive, ~50+ violations)

**Pros:**
- Balance between enforcement and pragmatism
- Catches important issues (exceptions, naming)
- Avoids var refactoring which is more stylistic

**Cons:**
- Leaves var usage inconsistent
- Still requires fixing ~30-60 violations

**Timeline:** 1-2 PRs

## Recommendation

**Option 1 (Aggressive Enforcement)** - **APPROVED**

This option applies all Category A + B + D rules immediately, enforcing:
- IDE0005 (unused usings)
- CA1031 (general exception catching)
- CA1716 (keyword naming conflicts)
- IDE0008 (explicit type declarations)
- Naming conventions (interfaces, PascalCase, camelCase)
- Readonly field enforcement
- Unused parameter removal

All violations (~80-110 total) will be fixed in a single PR to establish a strong baseline for code quality.

## Next Steps

**Approved Approach:** Iterative rule-by-rule implementation

Rather than applying all rules at once, we will implement each rule individually following this loop:

### Step 0: Consolidate EditorConfig Files

Before implementing individual rules, consolidate the .editorconfig hierarchy:

1. **Review** - Compare MediaSet.Api/.editorconfig with root .editorconfig
2. **Merge** - Ensure root .editorconfig has all necessary rules
3. **Remove** - Delete MediaSet.Api/.editorconfig so all projects inherit from root
4. **Verify** - Confirm MediaSet.Api project now uses root configuration

**Benefits:**
- Single source of truth for code analysis rules
- All .NET projects inherit same standards
- Individual projects can still override if needed (without `root = true`)
- Easier maintenance and consistency

### Implementation Loop (Repeat for Each Rule)

1. **Update `.editorconfig`** - Change rule severity (e.g., `none` → `error`, `suggestion` → `error`)
2. **Build & Assess** - Run `dotnet build` to identify violations for THIS rule only
3. **Fix Violations** - Address all errors for THIS rule
4. **Test** - Verify all tests pass with the new rule
5. **Commit** - Create a single commit for this rule's changes with message: `chore(analysis): enforce [RULE_ID] - [description]`

### Rule Implementation Order

1. ✅ **IDE0005** (unused usings) - Already enabled, 0 violations
2. ⏳ **CA1031** (catch general exceptions)
3. ⏳ **CA1716** (identifiers match keywords)
4. ⏳ **IDE0008/IDE0007** (explicit type vs var)
5. ⏳ **Naming: Interface prefix** (dotnet_naming_rule.interface_should_be_prefixed_with_i)
6. ⏳ **Naming: PascalCase types** (dotnet_naming_rule.types_should_be_pascal_case)
7. ⏳ **Naming: PascalCase members** (dotnet_naming_rule.non_field_members_should_be_pascal_case)
8. ⏳ **Naming: camelCase fields** (dotnet_naming_rule.private_or_internal_field_should_be_camel_case)
9. ⏳ **Readonly fields** (dotnet_style_readonly_field)
10. ⏳ **Unused parameters** (dotnet_code_quality_unused_parameters)

**Benefits of this approach:**
- Each commit is focused on a single rule change
- Easier to review individual rule impacts
- Can identify which rule causes issues if tests fail
- Incremental progress tracking
- Cleaner git history

**Final Step:** After all rules are implemented, update this document's status to "Implemented"

## References

- Microsoft Code Analysis Categories: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/categories
- EditorConfig Documentation: https://editorconfig.org/
- .NET Code Quality Rules: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/
- .NET Code Style Rules: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/
