using MediaSet.Api.Shared.Models;
using MediaSet.Api.Features.Entities.Services;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Plex;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic.FileIO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MediaSet.Api.Features.Entities.Endpoints;

internal static class EntityApi
{
    public static RouteGroupBuilder MapEntity<TEntity>(this IEndpointRouteBuilder routes) where TEntity : IEntity, new()
    {
        // get the app.Logger to be used for logging.
        var logger = ((WebApplication)routes).Logger;
        var entityType = $"{typeof(TEntity).Name}s";
        var group = routes.MapGroup($"/{entityType}");

        group.WithTags(entityType);

        group.MapGet("/", async (IEntityService<TEntity> entityService, CancellationToken cancellationToken) =>
        {
            logger.LogDebug("Listing all {entityType}", entityType);
            var result = await entityService.GetListAsync(cancellationToken);
            logger.LogDebug("Retrieved {count} {entityType}", result.Count(), entityType);
            return result;
        });

        group.MapGet("/search", async Task<Results<Ok<IEnumerable<TEntity>>, BadRequest<string>>> (IEntityService<TEntity> entityService, string searchText, string orderBy, CancellationToken cancellationToken) =>
        {
            try
            {
                logger.LogDebug("Searching {entityType} with query '{searchText}', orderBy {orderBy}", entityType, searchText, orderBy);
                var result = await entityService.SearchAsync(searchText, orderBy, cancellationToken);
                logger.LogDebug("Search returned {count} {entityType}", result.Count(), entityType);
                return TypedResults.Ok(result);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning("Invalid orderBy field for {entityType}: {message}", entityType, ex.Message);
                return TypedResults.BadRequest(ex.Message);
            }
        });

        group.MapGet("/pagedsearch", async Task<Results<Ok<PagedResult<TEntity>>, BadRequest<string>>> (
            IEntityService<TEntity> entityService,
            [Microsoft.AspNetCore.Mvc.FromServices] IPlexLookup? plexLookup,
            string searchText,
            string orderBy,
            string? plex = null,
            int page = 1,
            int pageSize = 75,
            CancellationToken cancellationToken = default) =>
        {
            try
            {
                logger.LogDebug("Paged searching {entityType} with query '{searchText}', orderBy {orderBy}, plex {plex}, page {page}, pageSize {pageSize}", entityType, searchText, orderBy, plex, page, pageSize);
                // Movies and music only; absent (and ignored) when Plex is not configured for the entity type
                IdFilter? idFilter = null;
                var plexFilterable = typeof(TEntity) == typeof(Movie) || typeof(TEntity) == typeof(Music);
                if (plexFilterable && plexLookup is not null && !string.IsNullOrWhiteSpace(plex))
                {
                    try
                    {
                        idFilter = typeof(TEntity) == typeof(Music)
                            ? await plexLookup.GetMusicFilterAsync(plex, cancellationToken)
                            : await plexLookup.GetMovieFilterAsync(plex, cancellationToken);
                    }
                    catch (ArgumentException ex)
                    {
                        logger.LogWarning("Invalid plex filter for {entityType}: {message}", entityType, ex.Message);
                        return TypedResults.BadRequest(ex.Message);
                    }
                }
                var result = await entityService.PagedSearchAsync(searchText, orderBy, page, pageSize, idFilter, cancellationToken);
                await AttachPlexSummariesAsync(result.Items, plexLookup, cancellationToken);
                logger.LogDebug("Paged search returned {count}/{total} {entityType}", result.Items.Count(), result.TotalCount, entityType);
                return TypedResults.Ok(result);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning("Invalid orderBy field for {entityType}: {message}", entityType, ex.Message);
                return TypedResults.BadRequest(ex.Message);
            }
        });

        group.MapGet("/{id}", async Task<Results<Ok<TEntity>, NotFound>> (
            IEntityService<TEntity> entityService,
            [Microsoft.AspNetCore.Mvc.FromServices] IPlexLookup? plexLookup,
            string id,
            CancellationToken cancellationToken) =>
        {
            logger.LogDebug("Fetching {entityType} by id {id}", entityType, id);
            var entity = await entityService.GetAsync(id, cancellationToken);
            if (entity is not null)
            {
                await AttachPlexAsync(entity, plexLookup, cancellationToken);
                return TypedResults.Ok(entity);
            }

            logger.LogWarning("{entityType} not found for id {id}", entityType, id);
            return TypedResults.NotFound();
        });

        group.MapPost("/", async Task<Results<Created<TEntity>, Created<JsonObject>, BadRequest<string>, ProblemHttpResult>> (
            IEntityWriteService<TEntity> writeService,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var request = await EntityRequestReader.ReadAsync<TEntity>(context.Request, "Entity is required and must have some data", cancellationToken);
                if (request.Error is not null)
                {
                    logger.LogError("Invalid create request for {entityType}: {error}", entityType, request.Error);
                    return TypedResults.BadRequest(request.Error);
                }

                var result = await writeService.CreateAsync(request.Entity!, request.Image, cancellationToken);
                switch (result.Status)
                {
                    case EntityWriteStatus.InvalidExternalId:
                        return InvalidExternalId();
                    case EntityWriteStatus.EmptyEntity:
                        return TypedResults.BadRequest("Entity is required and must have some data");
                }

                var entity = result.Entity!;
                var location = $"/{typeof(TEntity).Name}/{entity.Id}";
                if (result.ImageError is null)
                {
                    return TypedResults.Created(location, entity);
                }

                // The entity is saved; report the failed cover image alongside it.
                var jsonOptions = context.RequestServices
                    .GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>().Value.SerializerOptions;
                var body = JsonSerializer.SerializeToNode(entity, jsonOptions) as JsonObject ?? [];
                body["imageError"] = result.ImageError;
                return TypedResults.Created(location, body);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating {entityType}", entityType);
                return TypedResults.BadRequest("An error occurred while creating the entity.");
            }
        }).DisableAntiforgery();

        group.MapPut("/{id}", async Task<Results<Ok, Ok<ImageErrorResponse>, NotFound, BadRequest<string>, ProblemHttpResult>> (
            IEntityWriteService<TEntity> writeService,
            string id,
            HttpContext context,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var request = await EntityRequestReader.ReadAsync<TEntity>(context.Request, "Failed to deserialize entity", cancellationToken);
                if (request.Error is not null)
                {
                    logger.LogError("Invalid update request for {entityType}/{id}: {error}", entityType, id, request.Error);
                    return TypedResults.BadRequest(request.Error);
                }

                var result = await writeService.UpdateAsync(id, request.Entity!, request.Image, cancellationToken);
                switch (result.Status)
                {
                    case EntityWriteStatus.IdMismatch:
                        return TypedResults.BadRequest("Ids on the entity and the request do not match");
                    case EntityWriteStatus.InvalidExternalId:
                        return InvalidExternalId();
                    case EntityWriteStatus.NotFound:
                        return TypedResults.NotFound();
                }

                return result.ImageError is null
                    ? TypedResults.Ok()
                    : TypedResults.Ok(new ImageErrorResponse(result.ImageError));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating {entityType}/{id}", entityType, id);
                return TypedResults.BadRequest("An error occurred while updating the entity.");
            }
        }).DisableAntiforgery();

        group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (
            IEntityWriteService<TEntity> writeService,
            string id,
            CancellationToken cancellationToken) =>
        {
            await writeService.DeleteAsync(id, cancellationToken);
            return TypedResults.Ok();
        });

        group.MapDelete("/{id}/image", async Task<Results<NoContent, NotFound>> (
            IEntityWriteService<TEntity> writeService,
            string id,
            CancellationToken cancellationToken) =>
        {
            var status = await writeService.DeleteImageAsync(id, cancellationToken);
            return status == DeleteImageStatus.Deleted ? TypedResults.NoContent() : TypedResults.NotFound();
        });

        group.MapPost("/upload", async Task<Results<Ok<string>, BadRequest<string>>> (IEntityService<TEntity> entityService, IFormFile bookUpload, CancellationToken cancellationToken) =>
       {
           logger.LogInformation("Received {fileName} file to upload to {entityType}", bookUpload.FileName, entityType);
           IEnumerable<TEntity> newEntities;
           try
           {
               using Stream stream = bookUpload.OpenReadStream();

               using TextFieldParser parser = new(stream, System.Text.Encoding.UTF8);
               parser.TextFieldType = FieldType.Delimited;
               parser.SetDelimiters(";");
               parser.HasFieldsEnclosedInQuotes = true;
               IList<string>? headerFields = [];
               IList<string[]> dataFields = [];
               while (!parser.EndOfData)
               {
                   //Process header row
                   if (parser.LineNumber == 1)
                   {
                       headerFields = parser.ReadFields();
                       if (headerFields == null)
                       {
                           logger.LogError("No header fields are included in upload document");
                           return TypedResults.BadRequest("No header fields in upload document.");
                       }
                       logger.LogDebug("Header Fields: {headerFields}", string.Join(',', headerFields));
                   }
                   else
                   {
                       // process data rows
                       string[]? dataRow = parser.ReadFields();
                       if (dataRow != null)
                       {
                           logger.LogTrace("Entity Data: {dataRow}", string.Join(',', dataRow));
                           dataFields.Add(dataRow);
                       }
                   }
               }

               if (dataFields.Count == 0)
               {
                   logger.LogError("No data to upload");
                   return TypedResults.BadRequest("No data to upload.");
               }

               newEntities = UploadService.MapUploadToEntities<TEntity>(headerFields, dataFields);
               logger.LogInformation("Parsed {count} {entityType} from upload", newEntities.Count(), entityType);
               await entityService.BulkCreateAsync(newEntities, cancellationToken);
           }
           catch (Exception er)
           {
               logger.LogError(er, "Failed to save bulk create for {entityType}", entityType);
               return TypedResults.BadRequest(string.Format("Failed to save bulk create: {0}", er));
           }

           logger.LogInformation("Uploaded {count} new {entityType}", newEntities.Count(), entityType);
           return TypedResults.Ok(string.Format("Uploaded {0} new {1}", newEntities.Count(), entityType));
       })
       .DisableAntiforgery()
       .RequireRateLimiting("upload");

        return group;
    }

    private static ProblemHttpResult InvalidExternalId() => TypedResults.Problem(
        title: "Invalid ExternalId",
        detail: ExternalIdFormat.ErrorMessage,
        statusCode: StatusCodes.Status400BadRequest);

    // Paged search: adds the small Plex summary (no file paths) to each linked movie or album of the page, joined in one query.
    private static async Task AttachPlexSummariesAsync<TEntity>(IEnumerable<TEntity> entities, IPlexLookup? plexLookup, CancellationToken cancellationToken)
    {
        if (plexLookup is null)
        {
            return;
        }

        if (typeof(TEntity) == typeof(Music))
        {
            var albums = entities.OfType<Music>().Where(m => m.Id is not null).ToList();
            if (albums.Count == 0)
            {
                return;
            }

            var albumSummaries = await plexLookup.GetMusicSummariesAsync([.. albums.Select(m => m.Id!)], cancellationToken);
            foreach (var album in albums)
            {
                album.Plex = albumSummaries.GetValueOrDefault(album.Id!);
            }
            return;
        }

        var movies = entities.OfType<Movie>().Where(m => m.Id is not null).ToList();
        if (movies.Count == 0)
        {
            return;
        }

        var summaries = await plexLookup.GetMovieSummariesAsync([.. movies.Select(m => m.Id!)], cancellationToken);
        foreach (var movie in movies)
        {
            movie.Plex = summaries.GetValueOrDefault(movie.Id!);
        }
    }

    // Single lookup only: adds the stored Plex data (without file paths) to a linked movie or album. Plex not configured,
    // not enabled for movies, or no document for the id leaves the movie unchanged.
    private static async Task AttachPlexAsync(IEntity entity, IPlexLookup? plexLookup, CancellationToken cancellationToken)
    {
        if (plexLookup is null)
        {
            return;
        }

        if (entity is Movie { Id: not null } movie)
        {
            movie.Plex = await plexLookup.GetMovieDataAsync(movie.Id, cancellationToken);
        }
        else if (entity is Music { Id: not null } album)
        {
            album.Plex = await plexLookup.GetMusicDataAsync(album.Id, cancellationToken);
        }
    }
}
