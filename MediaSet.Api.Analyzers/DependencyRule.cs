using System;

namespace MediaSet.Api.Analyzers;

/// <summary>
/// Represents a namespace dependency rule for architectural enforcement.
/// </summary>
internal class DependencyRule
{
    /// <summary>
    /// The source namespace that is restricted from referencing the forbidden namespace.
    /// </summary>
    public string SourceNamespace { get; set; } = string.Empty;

    /// <summary>
    /// The namespace that cannot be referenced by the source namespace.
    /// </summary>
    public string ForbiddenNamespace { get; set; } = string.Empty;

    /// <summary>
    /// The error message to display when the rule is violated.
    /// Supports format placeholders: {0}=symbol name, {1}=containing namespace, {2}=full symbol, {3}=referenced namespace.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When true, allows references within the same feature slice.
    /// For example, MediaSet.Api.Features.Health.Services can reference MediaSet.Api.Features.Health.Models.
    /// </summary>
    public bool AllowSelf { get; set; } = true;

    /// <summary>
    /// The depth at which to compare namespaces when AllowSelf is true.
    /// For example, depth=4 means compare up to 'MediaSet.Api.Features.Health' level.
    /// Default is 4 for vertical slice features.
    /// </summary>
    public int SelfDepth { get; set; } = 4;
}
