using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using MediaSet.Api.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using NUnit.Framework;

namespace MediaSet.Api.Tests.Analyzers;

[TestFixture]
public class NamespaceDependencyAnalyzerTests
{
    private const string FeatureAType = "MediaSet.Api.Features.Alpha.Models";
    private const string FeatureBType = "MediaSet.Api.Features.Beta.Models";

    [Test]
    public void Analyzer_Always_DeclaresArch001ErrorDescriptor()
    {
        // Arrange
        var analyzer = new NamespaceDependencyAnalyzer();

        // Act
        var descriptors = analyzer.SupportedDiagnostics;

        // Assert
        Assert.That(descriptors, Has.Length.EqualTo(1));
        Assert.That(descriptors[0].Id, Is.EqualTo(NamespaceDependencyAnalyzer.DiagnosticId).And.EqualTo("ARCH001"));
        Assert.That(descriptors[0].DefaultSeverity, Is.EqualTo(DiagnosticSeverity.Error));
    }

    [Test]
    public async Task Analyze_InfrastructureUsesFeatureType_ReportsArch001()
    {
        // Arrange
        const string source = """
            namespace MediaSet.Api.Features.Alpha.Models { public class Thing { } }
            namespace MediaSet.Api.Infrastructure.Data
            {
                public class Repo { public MediaSet.Api.Features.Alpha.Models.Thing Value { get; set; } = new(); }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source, withRules: true);

        // Assert
        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics.Select(d => d.Id).Distinct(), Is.EqualTo(["ARCH001"]));
        Assert.That(diagnostics.Select(d => d.GetMessage()), Has.All.EqualTo("Infrastructure layer cannot reference Feature types"));
    }

    [Test]
    public async Task Analyze_SharedUsesFeatureUsingDirective_ReportsArch001()
    {
        // Arrange
        const string source = """
            namespace MediaSet.Api.Features.Alpha.Models { public class Thing { } }
            namespace MediaSet.Api.Shared.Util
            {
                using MediaSet.Api.Features.Alpha.Models;
                public class Helper { public Thing? Value { get; set; } }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source, withRules: true);

        // Assert
        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics.Select(d => d.Id).Distinct(), Is.EqualTo(["ARCH001"]));
        Assert.That(diagnostics.Select(d => d.GetMessage()), Has.All.EqualTo("Shared code cannot depend on specific features"));
    }

    [Test]
    public async Task Analyze_FeatureReferencesOtherFeature_ReportsArch001()
    {
        // Arrange
        var source = $$"""
            namespace {{FeatureAType}} { public class Thing { } }
            namespace {{FeatureBType}} { public class Other { public {{FeatureAType}}.Thing Value { get; set; } = new(); } }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source, withRules: true);

        // Assert
        Assert.That(diagnostics, Is.Not.Empty);
        Assert.That(diagnostics.Select(d => d.Id).Distinct(), Is.EqualTo(["ARCH001"]));
        Assert.That(diagnostics.Select(d => d.GetMessage()), Has.All.EqualTo("Features should not reference other features"));
    }

    [Test]
    public async Task Analyze_FeatureReferencesOwnSlice_ReportsNothing()
    {
        // Arrange
        const string source = """
            namespace MediaSet.Api.Features.Alpha.Models { public class Thing { } }
            namespace MediaSet.Api.Features.Alpha.Services
            {
                using MediaSet.Api.Features.Alpha.Models;
                public class Svc { public Thing Value { get; set; } = new(); }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source, withRules: true);

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task Analyze_FeatureUsesInfrastructureAndSharedTypes_ReportsNothing()
    {
        // Arrange
        const string source = """
            namespace MediaSet.Api.Infrastructure.Data { public class Repo { } }
            namespace MediaSet.Api.Shared.Util { public class Helper { } }
            namespace MediaSet.Api.Features.Alpha.Services
            {
                using MediaSet.Api.Infrastructure.Data;
                using MediaSet.Api.Shared.Util;
                public class Svc { public Repo R { get; set; } = new(); public Helper H { get; set; } = new(); }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source, withRules: true);

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    [Test]
    public async Task Analyze_ViolationWithoutConfiguredRules_ReportsNothing()
    {
        // Arrange
        const string source = """
            namespace MediaSet.Api.Features.Alpha.Models { public class Thing { } }
            namespace MediaSet.Api.Infrastructure.Data
            {
                public class Repo { public MediaSet.Api.Features.Alpha.Models.Thing Value { get; set; } = new(); }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source, withRules: false);

        // Assert
        Assert.That(diagnostics, Is.Empty);
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source, bool withRules)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Where(p => Path.GetFileName(p) is "System.Private.CoreLib.dll" or "System.Runtime.dll")
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToArray();
        var compilation = CSharpCompilation.Create(
            "AnalyzerSample",
            [tree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var options = new AnalyzerOptions(
            [],
            new GlobalOptionsProvider(withRules ? LoadRepositoryRules() : []));
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new NamespaceDependencyAnalyzer());

        var result = await compilation.WithAnalyzers(analyzers, options).GetAnalyzerDiagnosticsAsync();

        // Guard against a vacuous pass: the sample itself must compile, and analyzer crashes (AD0001) must not hide.
        Assert.That(compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error), Is.Empty);
        Assert.That(result.Where(d => d.Id != "ARCH001"), Is.Empty);
        return result;
    }

    /// <summary>Reads the build_metadata keys from the repository's real .globalconfig so the shipped rules are tested.</summary>
    private static Dictionary<string, string> LoadRepositoryRules()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, ".globalconfig")))
        {
            dir = dir.Parent;
        }
        Assert.That(dir, Is.Not.Null, "Could not locate .globalconfig above the test output directory");

        var values = new Dictionary<string, string>();
        foreach (var line in File.ReadAllLines(Path.Combine(dir!.FullName, ".globalconfig")))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed[0] is '#' or ';' || !trimmed.StartsWith("build_metadata.", StringComparison.Ordinal))
            {
                continue;
            }

            var idx = trimmed.IndexOf('=');
            values[trimmed[..idx].Trim()] = trimmed[(idx + 1)..].Trim();
        }
        Assert.That(values, Is.Not.Empty);
        return values;
    }

    private sealed class GlobalOptionsProvider(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptionsProvider
    {
        private readonly Options _global = new(values);
        public override AnalyzerConfigOptions GlobalOptions => _global;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _global;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _global;
    }

    private sealed class Options(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            var found = values.TryGetValue(key, out var v);
            value = v;
            return found;
        }
    }
}
