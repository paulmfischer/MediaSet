using System.ComponentModel.DataAnnotations;

namespace MediaSet.Api.Features.Logs.Models;

/// <summary>
/// Represents a log event sent from the client-side UI.
/// </summary>
public record ClientLogEvent(
    string Level,
    [MaxLength(1024)] string Message,
    DateTimeOffset Timestamp,
    Dictionary<string, object?>? Properties);
