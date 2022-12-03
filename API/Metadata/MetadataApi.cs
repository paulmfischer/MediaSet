using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace API;

internal static class MetadataApi
{
    public static RouteGroupBuilder MapMetadata(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/metadata");
        group.WithTags("Metadata");

        // Format
        group.MapGet("/format", async (MediaSetContext context) =>
        {
            return await context.Formats.AsNoTracking().ToListAsync();
        });

        group.MapGet("/format/{id}", async Task<Results<Ok<Format>, NotFound>> (MediaSetContext context, int id) =>
        {
            return await context.Formats.FindAsync(id) switch
            {
                Format format => TypedResults.Ok(format),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/format", async Task<Created<Format>> (MediaSetContext context, Format format) =>
        {
            var newFormat = new Format
            {
                MediaType = format.MediaType,
                Name = format.Name,
            };

            context.Formats.Add(newFormat);
            await context.SaveChangesAsync();

            return TypedResults.Created($"/format/{newFormat.Id}", newFormat);
        });

        group.MapPut("/format/{id}", async Task<Results<Ok, NotFound, BadRequest>> (MediaSetContext context, int id, Format format) =>
        {
            if (id != format.Id)
            {
                return TypedResults.BadRequest();
            }

            var rowsAffected = await context.Formats
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(f => f.Name, format.Name)
                           .SetProperty(f => f.MediaType, format.MediaType)
                );

            if (rowsAffected == 0)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok();
        });

        // Genre
        group.MapGet("/genre", async (MediaSetContext context) =>
        {
            return await context.Genres.AsNoTracking().ToListAsync();
        });

        group.MapGet("/genre/{id}", async Task<Results<Ok<Genre>, NotFound>> (MediaSetContext context, int id) =>
        {
            return await context.Genres.FindAsync(id) switch
            {
                Genre genre => TypedResults.Ok(genre),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/genre", async Task<Created<Genre>> (MediaSetContext context, Genre genre) =>
        {
            var newGenre = new Genre { Name = genre.Name };

            context.Genres.Add(newGenre);
            await context.SaveChangesAsync();

            return TypedResults.Created($"/genre/{newGenre.Id}", newGenre);
        });

        group.MapPut("/genre/{id}", async Task<Results<Ok, NotFound, BadRequest>> (MediaSetContext context, int id, Genre genre) =>
        {
            if (id != genre.Id)
            {
                return TypedResults.BadRequest();
            }

            var rowsAffected = await context.Genres
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(f => f.Name, genre.Name)
                );

            if (rowsAffected == 0)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok();
        });

        // Studio
        group.MapGet("/studio", async (MediaSetContext context) =>
        {
            return await context.Studios.AsNoTracking().ToListAsync();
        });

        group.MapGet("/studio/{id}", async Task<Results<Ok<Studio>, NotFound>> (MediaSetContext context, int id) =>
        {
            return await context.Studios.FindAsync(id) switch
            {
                Studio studio => TypedResults.Ok(studio),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/studio", async Task<Created<Studio>> (MediaSetContext context, Studio studio) =>
        {
            var newStudio = new Studio { Name = studio.Name };

            context.Studios.Add(newStudio);
            await context.SaveChangesAsync();

            return TypedResults.Created($"/studio/{newStudio.Id}", newStudio);
        });

        group.MapPut("/studio/{id}", async Task<Results<Ok, NotFound, BadRequest>> (MediaSetContext context, int id, Studio studio) =>
        {
            if (id != studio.Id)
            {
                return TypedResults.BadRequest();
            }

            var rowsAffected = await context.Studios
                .Where(f => f.Id == id)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(f => f.Name, studio.Name)
                );

            if (rowsAffected == 0)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok();
        });

        return group;
    }
}