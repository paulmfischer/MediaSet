namespace MediaSet.Api.Models;

/// <summary>
/// Represents a log event sent from the client-side UI.
/// </summary>
public record ClientLogEvent(
    string Level,
    string Message,
    DateTimeOffset Timestamp,
    Dictionary<string, object?>? Properties);
