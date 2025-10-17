using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MediaSet.Api.Metadata;

internal static class HealthApi
{
    public static RouteGroupBuilder MapHealth(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/health");

        group.WithTags("Health");

        group.MapGet("/", async (IDatabaseService databaseService, IOptions<MediaSetDatabaseSettings> settings) =>
        {
            var dbName = settings.Value.DatabaseName ?? string.Empty;
            var dbStatus = "down";

            try
            {
                // Obtain database from any collection; Database property points to the configured IMongoDatabase
                var collection = databaseService.GetCollection<BsonDocument>();
                var database = collection.Database;
                // Ping command
                await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
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
