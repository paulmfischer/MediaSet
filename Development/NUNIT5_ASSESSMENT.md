# NUnit 5, adapter 6 and Test SDK 18 assessment

Assessment for [#659](https://github.com/paulmfischer/MediaSet/issues/659) under
[#652](https://github.com/paulmfischer/MediaSet/issues/652). Assessed on 2026-11-07. No package
version in the repository was changed by this assessment; every measurement below came from a
disposable copy of the repository outside the working tree.

## Decision

| Component | Declared / resolved now | Candidate | Age on 2026-11-07 | Decision |
|-----------|-------------------------|-----------|-------------------|----------|
| NUnit | 4.6.1 / 4.6.1 | 5.0.0 (latest stable) | 41 days (2026-09-27) | Adopt |
| NUnit3TestAdapter | 4.6.0 / 4.6.0 | 6.3.1 (latest stable, hotfix of 6.3.0) | 29 days (2026-10-09) | Adopt |
| Microsoft.NET.Test.Sdk | 17.14.1 / 17.14.1 | 18.11.0 (latest stable) | 25 days (2026-10-13) | Adopt |
| NUnit.Analyzers (new) | not used | 4.15.0 (latest stable; 4.14+ flags unawaited assertions) | 56 days (2026-09-12) | Adopt, as an analyzer guard |
| Test runner | VSTest (`dotnet test`) | Microsoft.Testing.Platform | n/a | Keep VSTest; MTP deferred |

The three packages move together in one change: the async assertion migration is only needed
(and only safe to verify) once NUnit 5 is in, and the analyzer rule that guards it needs the
same change. Versions and dates came from the nuget.org registration API; all are listed, not
deprecated, and `dotnet list package --vulnerable --include-transitive` reports nothing for
either the current or the probe package set. Revalidate versions and age at implementation.

### Evidence

- **Target frameworks.** NUnit 5.0.0 targets net462, net8.0 and net10.0 (net6.0 was dropped).
  The test project targets net10.0, so it is supported. Adapter 6.3.1 and Test SDK 18.11.0 both
  carry net8.0 and net462 dependency groups and ran on net10.0 here.
- **Adapter release notes.** 6.3.1 only fixes a Visual Studio Test Explorer regression for
  nullable `[TestCase]` parameters under Microsoft.Testing.Platform
  ([nunit3-vs-adapter#1504](https://github.com/nunit/nunit3-vs-adapter/issues/1504)); the repo has no such
  case and runs VSTest, so 6.3.1 is preferred over 6.3.0 regardless.
- **NUnit 5 breaking changes**
  ([docs](https://docs.nunit.org/articles/nunit/V5BreakingChanges.html)): `TestDelegate`,
  `AsyncTestDelegate` and `ActualValueDelegate<T>` removed; `Assert.That(Func<T>, ...)` now
  evaluates the function and constrains its result; `Assert.ThrowsAsync`, `CatchAsync` and
  `DoesNotThrowAsync` return `Task`/`Task<T>` and unawaited calls are never evaluated;
  `Is.SameAs` is limited to reference types; legacy `StringAssert`/`CollectionAssert` return to
  `NUnit.Framework`; platform names `NET`/`DotNET` mean modern .NET; `Has.One.Matches`
  predicates are nullable; `CollectionTally` obsolete; forced stop removed.
- **Transitive impact of adapter 6.3.1.** It adds Microsoft.Testing.Platform 2.4.1,
  Microsoft.Testing.Extensions.VSTestBridge 2.4.1, Microsoft.Testing.Platform.MSBuild 2.4.1,
  Microsoft.Testing.Extensions.Telemetry 2.4.1, Microsoft.Testing.Extensions.TrxReport.Abstractions 2.4.1,
  Microsoft.TestPlatform.AdapterUtilities 18.10.1 (one minor behind TestHost 18.11.0) and
  Microsoft.Extensions.DependencyModel 10.0.14 to the test project only; the shipped API image
  does not reference them. Test SDK 18.11.0 moves TestHost, ObjectModel and CodeCoverage to 18.11.0.
  `dotnet test` still ran as VSTest (output format, `--filter`, `test.runsettings` all unchanged).
- **Nothing in NUnit 5 touches the repo beyond async assertions.** The test project has no
  `Func<T>` actual-value assertions, no `Is.SameAs` on value types, no `CollectionAssert`/
  `StringAssert`/`FileAssert`, no `TestDelegate` types, no `Has.One.Matches`, no platform
  attributes. The two `Assert.Multiple` calls (`ImageStatsServiceTests` line 91 and
  `ImageLookupServiceTests` line 206) use synchronous lambdas with plain `Assert.That`, which is
  unchanged. Moq's `.ThrowsAsync(...)` calls on mock setups are unrelated to `Assert.ThrowsAsync`
  and need no change.

## Async assertion audit

38 `Assert.ThrowsAsync`/`CatchAsync`/`DoesNotThrowAsync` calls across 13 files (the issue quoted 37
across 14; this count is from `main` at e49b383 and includes one `CatchAsync` and two
`DoesNotThrowAsync`). There are no `Assert.ThatAsync` calls today. Paths are under
`MediaSet.Api.Tests/`. "void->async" means the test method is `void` and must become
`async Task`; "already async" means it is already `async Task` and only needs the `await`.
Every call needs `await`; sites that use the returned exception (`var ex = ...`) become
`var ex = await Assert.ThrowsAsync<...>(...)`.

| File | Calls | Test signatures |
|------|-------|-----------------|
| `Infrastructure/Storage/ImageServiceTests.cs` | 12 (lines 164-509; 11 use the result, 1 does not) | 12 void->async |
| `Infrastructure/Storage/ImageIngestionTests.cs` | 6 (88, 104, 140, 169, 237, 251 `CatchAsync`) | 4 void->async; 2 already async (88, 140) |
| `Infrastructure/Storage/SsrfGuardTests.cs` | 5 (87, 97, 111, 130, 149) | 3 void->async; 2 already async (97, 130 are `DoesNotThrowAsync`) |
| `Features/Images/Services/ImageRetryProcessingTests.cs` | 2 (109, 149) | 2 void->async |
| `Features/Metadata/Services/MetadataServiceTests.cs` | 2 (277, 288) | 2 void->async |
| `Infrastructure/DataAccess/EntityServiceTests.cs` | 2 (403, 411) | 2 void->async |
| `Infrastructure/Lookup/Clients/Igdb/IgdbClientTests.cs` | 2 (126, 219) | 2 void->async |
| `Infrastructure/Lookup/Clients/Tmdb/TmdbTvClientTests.cs` | 2 (118, 132) | 2 void->async |
| `Features/Images/Services/ProviderOutcomeTests.cs` | 1 (154) | already async |
| `Features/Images/Services/ImageLookupServiceTests.cs` | 1 (160) | void->async |
| `Features/Images/Services/ImageManagementServiceTests.cs` | 1 (238) | void->async |
| `Infrastructure/Lookup/Clients/OpenLibrary/OpenLibraryClientTests.cs` | 1 (566) | void->async |
| `Infrastructure/Lookup/Strategies/MovieLookupStrategyTests.cs` | 1 (426) | void->async |

Totals: 33 void->async signatures, 5 already async, 38 awaits. Sync `Assert.Throws<T>` calls
(`TvArtworkLookupTests`, `ImageIngestionTests` line 213, `RuntimeConverterTests`,
`StringExtensionsTests`, `ImageFallbackConfidenceTests`, `ProviderOutcomeTests`,
`ImageProviderFailureTests`) take synchronous delegates and are unaffected.

What the compiler does with the unmigrated code under NUnit 5 (measured in the probe):

- 13 sites that use the returned exception fail to compile (17 CS1061 errors, `Task<T?>` has no
  `Message`, `ParamName` or `StatusCode`).
- 5 sites in already-async methods fail to compile (CS4014, because `TreatWarningsAsErrors` is on).
- The remaining 20 sites in `void` methods that ignore the result compile cleanly and are no
  longer evaluated. This is the dangerous group: the build and the test run stay green.

## Probe

Method: a copy of the repository under the session scratchpad
(`.../scratchpad/probe`, not committed), with the three packages bumped, then migrated with the
mechanical edit described above (an `await` on each of the 38 calls and `void` to `async Task`
where needed), plus a probe fixture with deliberate cases.

| Check | Baseline (NUnit 4.6.1 / 4.6.0 / 17.14.1) | Probe (5.0.0 / 6.3.1 / 18.11.0) |
|-------|------------------------------------------|---------------------------------|
| `dotnet test` result | 782 passed, 1 skipped, 783 total | 782 passed, 1 skipped, 783 total after migration |
| Discovered test names (`--list-tests`) | 783 | 783, identical sorted list (`diff` empty) |
| Duration | about 19 s | about 19 s |
| Unmigrated test code | compiles, passes | 22 compile errors at 18 sites (17 CS1061, 5 CS4014) |
| Skipped test | `GetItemByCodeAsync_ApproachingMinuteLimit_PausesProactively` | same |
| `test.runsettings` (`ASPNETCORE_ENVIRONMENT=Testing`) | honored | honored |

Probe fixture (11 tests, run in addition to the 783):

- Awaited passing `ThrowsAsync` and `DoesNotThrowAsync`: passed.
- Awaited failing cases (wrong exception type, no exception, wrong message, `DoesNotThrowAsync`
  that throws, `ThatAsync` with a wrong value): all 5 reported as failures with the expected
  messages, so awaited async assertions do execute and fail correctly.
- Unawaited cases, written to be impossible to pass if evaluated: results are not deterministic.
  A call whose task is already completed (`Task.FromException`) is reported as a failure.
  Unawaited calls that complete later (`Task.Yield`, `Task.Delay(300)`) were reported as failures in
  some runs and as passes in others (6 repeated runs of the 4 unawaited tests gave 1 or 2
  failures, alternating). In other words an unawaited assertion is a coin flip that can leave a
  broken test green. This is why a compile-time guard is warranted rather than relying on
  `dotnet test`.
- With NUnit.Analyzers 4.15.0 added, every unawaited probe call is reported as
  `NUnit2059: Method 'ThrowsAsync' returns a Task and is not being observed` (also for
  `ThatAsync`). The rule is an error in this repo because `TreatWarningsAsErrors` is on.
- The same analyzer also reports `NUnit1032` for the real suite: the `_exporter` field in
  `Observability/TracePropagationAndLogCorrelationTests.cs` is created in `[SetUp]` and never
  disposed. A `[TearDown]` that calls `_exporter.Dispose()` fixes it (verified: 782 passed, 1
  skipped, no other analyzer diagnostics). Do not suppress the rule.

## CI and verification impacts

- CI (`.github/workflows/pr-checks.yml`) runs `dotnet restore MediaSet.sln`, `dotnet build
  MediaSet.sln --no-restore` and `dotnet test MediaSet.Api.Tests/MediaSet.Api.Tests.csproj
  --no-restore`. These commands need no change; the analyzer and awaits are enforced by the
  existing build step.
- The restore adds the transitive packages listed above (test project only). No vulnerability
  was reported for them.
- Expected passing count after the migration is 782 passed, 1 skipped (783 total), with an
  identical discovered test list. Any difference needs explaining before merge.
- **Runner choice: stay on VSTest.** The probe shows discovery, results, filters and runsettings
  working under VSTest with the new packages, and nothing in NUnit 5 or Test SDK 18 requires
  Microsoft.Testing.Platform. Adopting MTP would change the `dotnet test` invocation and its
  CI semantics (for example the .NET 10 `global.json` test-runner setting, option names, result
  and coverage output) and is a runner architecture change that this assessment does not
  justify. Revisit it only as a separate decision.

## Draft implementation ticket

Filed as a single ticket because the three packages and the guard are one reviewable
compatibility boundary.

### Migrate the API test project to NUnit 5, adapter 6, Test SDK 18 and the unawaited-assertion analyzer

**Parent:** #652. Assessment: #659 (`Development/NUNIT5_ASSESSMENT.md`).

**What to build.** In `MediaSet.Api.Tests/MediaSet.Api.Tests.csproj`, move NUnit 4.6.1 to 5.0.0,
NUnit3TestAdapter 4.6.0 to 6.3.1 and Microsoft.NET.Test.Sdk 17.14.1 to 18.11.0, and add
NUnit.Analyzers 4.15.0 (`PrivateAssets=all`). Await all 38 `Assert.ThrowsAsync`/`CatchAsync`/
`DoesNotThrowAsync` calls and change the 33 `void` tests that contain them to `async Task`, per the
audit table. Add a `[TearDown]` disposing `_exporter` in
`Observability/TracePropagationAndLogCorrelationTests.cs` for NUnit1032. Keep VSTest; do not
adopt Microsoft.Testing.Platform. Hold: Moq, Bogus, Mvc.Testing and all non-test packages.

**Acceptance criteria**
- [ ] Revalidate target versions, release age (at least 14 days), advisories and transitive
      packages on the day; record declared and resolved versions before and after.
- [ ] All 38 async assertions are awaited and no `void` test contains one; no unawaited
      `Assert.*Async` remains (`grep` plus NUnit2059 clean).
- [ ] `dotnet build` has no analyzer suppressions or `NoWarn` additions for NUnit rules;
      NUnit2059 and NUnit1032 are clean.
- [ ] `dotnet test` reports the same discovered names as before (`--list-tests` diff empty) and
      the same result counts (782 passed, 1 skipped at the time of assessment, or an explained
      difference).
- [ ] Demonstrate, without committing it, that an awaited deliberately failing
      `Assert.ThrowsAsync` is reported as a failure and that an unawaited one is rejected by the
      build.
- [ ] CI commands are unchanged and pass.

**Out of scope:** Microsoft.Testing.Platform or any runner change, other package updates,
rewriting test logic.
