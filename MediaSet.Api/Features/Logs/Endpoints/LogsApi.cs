using MediaSet.Api.Features.Logs.Models;
namespace MediaSet.Api.Features.Logs.Endpoints;

/// <summary>
/// Maps HTTP endpoints for client log ingestion.
/// </summary>
public static class LogsApi
{
    /// <summary>
    /// Accepts a log event from the client-side UI and emits it through the server-side logger.
    /// This allows client logs to be enriched with server context (Application, Environment)
    /// and routed to the configured external logger (Seq, etc.).
    /// </summary>
    public static void MapLogs(this WebApplication app)
    {
        var group = app.MapGroup("/api/logs")
            .WithName("Logs");

        group.MapPost("/", HandleClientLog)
            .WithName("LogClientEvent")
            .WithDescription("Accept a log event from the client and route to the configured logger")
            .Accepts<ClientLogEvent>("application/json")
            .Produces(StatusCodes.Status202Accepted)
            .RequireRateLimiting("client-logs");
    }

    private static IResult HandleClientLog(
        ClientLogEvent logEvent,
        ILogger<Program> logger,
        IHostEnvironment environment)
    {
        if (!Enum.TryParse<LogLevel>(logEvent.Level, ignoreCase: true, out var logLevel))
        {
            return Results.BadRequest($"Invalid log level: {logEvent.Level}");
        }

        // Merge client properties with server-enriched metadata
        var scopeProperties = new Dictionary<string, object?>(logEvent.Properties ?? new Dictionary<string, object?>())
        {
            ["Application"] = "MediaSet.Remix",
            ["Environment"] = environment.EnvironmentName,
        };

        using (logger.BeginScope(scopeProperties))
        {
            logger.Log(logLevel, "{Message}", logEvent.Message);
        }

        return Results.Accepted();
    }
}
