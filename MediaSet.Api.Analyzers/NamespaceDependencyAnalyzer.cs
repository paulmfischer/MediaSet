using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace MediaSet.Api.Analyzers;

/// <summary>
/// Analyzes namespace dependencies to enforce architectural rules.
/// Prevents violations such as Infrastructure depending on Features, or Features cross-referencing each other.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NamespaceDependencyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ARCH001";
    private const string Category = "Architecture";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Namespace dependency violation",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Enforces namespace dependency rules to maintain clean architecture.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var options = compilationContext.Options.AnalyzerConfigOptionsProvider;
            var rules = LoadRules(options.GlobalOptions);

            // Debug: Report if no rules are loaded
            if (rules.Count == 0)
            {
                // No rules configured - analyzer is loaded but not configured
                return;
            }

            compilationContext.RegisterSyntaxNodeAction(nodeContext =>
                AnalyzeNode(nodeContext, rules),
                SyntaxKind.UsingDirective,
                SyntaxKind.QualifiedName,
                SyntaxKind.IdentifierName);
        });
    }

    private static List<DependencyRule> LoadRules(AnalyzerConfigOptions globalOptions)
    {
        var rules = new List<DependencyRule>();

        // MSBuild passes ItemGroup items as semicolon-separated properties
        // with indexed suffixes for metadata
        int ruleIndex = 0;
        while (true)
        {
            var identityKey = $"build_metadata.NamespaceDependencyRule.Identity_{ruleIndex}";
            if (!globalOptions.TryGetValue(identityKey, out var identity))
                break;

            var sourceKey = $"build_metadata.NamespaceDependencyRule.SourceNamespace_{ruleIndex}";
            var forbiddenKey = $"build_metadata.NamespaceDependencyRule.ForbiddenNamespace_{ruleIndex}";
            var messageKey = $"build_metadata.NamespaceDependencyRule.Message_{ruleIndex}";
            var allowSelfKey = $"build_metadata.NamespaceDependencyRule.AllowSelf_{ruleIndex}";
            var selfDepthKey = $"build_metadata.NamespaceDependencyRule.SelfDepth_{ruleIndex}";

            if (globalOptions.TryGetValue(sourceKey, out var sourceNamespace) &&
                globalOptions.TryGetValue(forbiddenKey, out var forbiddenNamespace) &&
                !string.IsNullOrWhiteSpace(sourceNamespace) &&
                !string.IsNullOrWhiteSpace(forbiddenNamespace))
            {
                globalOptions.TryGetValue(messageKey, out var message);
                globalOptions.TryGetValue(allowSelfKey, out var allowSelfStr);
                globalOptions.TryGetValue(selfDepthKey, out var selfDepthStr);

                var allowSelf = bool.TryParse(allowSelfStr, out var parsedAllowSelf) ? parsedAllowSelf : true;
                var selfDepth = int.TryParse(selfDepthStr, out var parsedDepth) ? parsedDepth : 4;

                rules.Add(new DependencyRule
                {
                    SourceNamespace = sourceNamespace.Trim(),
                    ForbiddenNamespace = forbiddenNamespace.Trim(),
                    Message = message?.Trim() ??
                        $"'{sourceNamespace}' cannot reference '{forbiddenNamespace}'",
                    AllowSelf = allowSelf,
                    SelfDepth = selfDepth
                });
            }

            ruleIndex++;
        }

        return rules;
    }

    private static void AnalyzeNode(
        SyntaxNodeAnalysisContext context,
        List<DependencyRule> rules)
    {
        var node = context.Node;
        var semanticModel = context.SemanticModel;

        // Get the containing namespace
        var containingNamespace = GetContainingNamespace(node);
        if (containingNamespace == null)
            return;

        // Check what's being referenced
        var symbolInfo = semanticModel.GetSymbolInfo(node);
        var symbol = symbolInfo.Symbol;

        if (symbol == null)
            return;

        var referencedNamespace = symbol.ContainingNamespace?.ToDisplayString();
        if (referencedNamespace == null)
            return;

        // Check against all rules
        foreach (var rule in rules)
        {
            if (!containingNamespace.StartsWith(rule.SourceNamespace))
                continue;

            if (!referencedNamespace.StartsWith(rule.ForbiddenNamespace))
                continue;

            // If AllowSelf is true, check if they're in the same feature slice
            if (rule.AllowSelf && AreInSameFeatureSlice(containingNamespace, referencedNamespace, rule.SelfDepth))
                continue;

            var message = string.Format(rule.Message,
                symbol.Name,
                containingNamespace,
                symbol.ToDisplayString(),
                referencedNamespace);

            var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), message);
            context.ReportDiagnostic(diagnostic);
            return; // Report only first violation per node
        }
    }

    private static string? GetContainingNamespace(SyntaxNode node)
    {
        var namespaceDecl = node.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return namespaceDecl?.Name.ToString();
    }

    /// <summary>
    /// Checks if two namespaces belong to the same feature slice by comparing up to a specific depth.
    /// For example, with depth=4:
    /// - MediaSet.Api.Features.Health.Services and MediaSet.Api.Features.Health.Models → same slice
    /// - MediaSet.Api.Features.Health.Services and MediaSet.Api.Features.Statistics.Models → different slices
    /// </summary>
    private static bool AreInSameFeatureSlice(string namespace1, string namespace2, int depth)
    {
        if (namespace1 == namespace2)
            return true;

        var parts1 = namespace1.Split('.');
        var parts2 = namespace2.Split('.');

        // Compare up to the specified depth
        var compareDepth = Math.Min(Math.Min(parts1.Length, parts2.Length), depth);

        for (int i = 0; i < compareDepth; i++)
        {
            if (parts1[i] != parts2[i])
                return false;
        }

        return true;
    }
}
