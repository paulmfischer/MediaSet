using MediaSet.Api.Features.Health.Services;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Features.Entities.Models;
using MediaSet.Api.Features.Health.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MediaSet.Api.Features.Health.Endpoints;

internal static class HealthApi
{
    public static RouteGroupBuilder MapHealth(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/health");

        group.WithTags("Health");

        group.MapGet("/", async (IDatabaseService databaseService, IVersionService versionService, IOptions<MediaSetDatabaseSettings> settings, CancellationToken cancellationToken) =>
        {
            var dbName = settings.Value.DatabaseName ?? string.Empty;
            var dbStatus = "down";

            try
            {
                logger.LogInformation("Pinging MongoDB database: {DatabaseName}", dbName);
                // Obtain database from any collection; Database property points to the configured IMongoDatabase
                var collection = databaseService.GetCollection<BsonDocument>();
                var database = collection.Database;
                // Ping command
                await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
                dbStatus = "up";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "MongoDB ping failed");
                dbStatus = "down";
            }

            var overall = dbStatus == "up" ? "healthy" : "degraded";

            return Results.Ok(new
            {
                status = overall,
                version = versionService.GetVersion(),
                commit = versionService.GetCommitSha(),
                buildTime = versionService.GetBuildTime(),
                timestamp = DateTime.UtcNow,
                database = new
                {
                    name = dbName,
                    status = dbStatus
                }
            });
        });

        return group;
    }
}
