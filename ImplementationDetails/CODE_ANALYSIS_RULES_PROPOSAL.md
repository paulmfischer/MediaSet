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

### Category B: Important Quality Rules → `warning` (Build Succeeds with Warnings)

| Rule | Current | Proposed | Rationale |
|------|---------|----------|-----------|
| **CA1031** | `none` | `warning` | Do not catch general exceptions - Found 20+ violations; helps identify poor error handling patterns |
| **CA1716** | `none` | `warning` | Identifiers shouldn't match keywords - Prevents naming confusion with language keywords |
| **IDE0008** | N/A | `warning` | Use explicit type declaration - Discourages var for non-obvious types, improves code clarity |

**Estimated Impact:** ⚠️ **70-80 violations**
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

### Category D: Style Rules → Upgrade to `warning`

| Rule | Current | Proposed | Rationale |
|------|---------|----------|-----------|
| **Naming conventions** (4 rules) | `suggestion` | `warning` | Enforce interface prefix (I), PascalCase for types, camelCase for fields - Standard .NET conventions |
| **dotnet_style_readonly_field** | `suggestion` | `warning` | Mark fields readonly when possible - Improves immutability and thread safety |
| **dotnet_code_quality_unused_parameters** | `suggestion` | `warning` | Remove unused parameters - Indicates dead code or oversight |

**Estimated Impact:** ⚠️ **10-30 violations**
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
- ✅ 0 violations expected

**Build Warnings:**
- ⚠️ 80-110 total violations across all categories
- Most violations are in CA1031 (general exceptions) and IDE0008 (var usage)

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
- CA1031 general exceptions (warning)
- CA1716 keyword naming (warning)
- Naming conventions (warning)
- Readonly fields (warning)
- Unused parameters (warning)

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

**Option 3 (Moderate Balanced)** is recommended because:

1. **Addresses critical quality issues** - Catches poor exception handling and naming problems
2. **Practical** - Avoids large-scale var refactoring which is more stylistic than functional
3. **Manageable** - ~30-60 violations is reasonable to fix in one PR
4. **Room for future** - Can always add IDE0008 later if desired

## Next Steps

1. **Approve Option** - Select implementation option (1, 2, or 3)
2. **Apply Rules** - Update `.editorconfig` with approved severity changes
3. **Build & Assess** - Run `dotnet build` to identify actual violations
4. **Fix Violations** - Address all errors and warnings
5. **Test** - Verify all tests pass with new rules
6. **Document** - Update development docs with new standards
7. **Commit** - Create PR with changes

## References

- Microsoft Code Analysis Categories: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/categories
- EditorConfig Documentation: https://editorconfig.org/
- .NET Code Quality Rules: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/
- .NET Code Style Rules: https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/style-rules/
