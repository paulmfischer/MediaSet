using MediaSet.Api.Bindings;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaSet.Api.Metadata;

internal static class MetadataApi
{
    public static RouteGroupBuilder MapMetadata(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/metadata");

        group.WithTags("Metadata");

        group.MapGet("/formats/{media}", async (IMetadataService metadataService, [FromRoute] Parameter<MediaTypes> media) =>
        {
            MediaTypes mediaTypes = media;
            logger.LogInformation("Requesting formats for {media}", mediaTypes);
            return mediaTypes switch
            {
                MediaTypes.Books => await metadataService.GetBookFormats(),
                MediaTypes.Movies => await metadataService.GetMovieFormats(),
                MediaTypes.Games => await metadataService.GetGameFormats(),
                MediaTypes.Musics => await metadataService.GetMusicFormats(),
                _ => throw new ArgumentException($"Media Type of {mediaTypes} is not supported")
            };
        });

        group.MapGet("/genres/{media}", async (IMetadataService metadataService, [FromRoute] Parameter<MediaTypes> media) =>
        {
            MediaTypes mediaTypes = media;
            logger.LogInformation("Requesting genres for {media}", mediaTypes);
            return mediaTypes switch
            {
                MediaTypes.Books => await metadataService.GetBookGenres(),
                MediaTypes.Movies => await metadataService.GetMovieGenres(),
                MediaTypes.Games => await metadataService.GetGameGenres(),
                MediaTypes.Musics => await metadataService.GetMusicGenres(),
                _ => throw new ArgumentException($"Media Type of {mediaTypes} is not supported")
            };
        });

        group.MapGet("/studios", async (IMetadataService metadataService) =>
        {
            var studios = await metadataService.GetMovieStudios();
            logger.LogInformation("Returning {count} studios", studios.Count());
            return studios;
        });
        group.MapGet("/authors", async (IMetadataService metadataService) =>
        {
            var authors = await metadataService.GetBookAuthors();
            logger.LogInformation("Returning {count} authors", authors.Count());
            return authors;
        });
        group.MapGet("/publishers", async (IMetadataService metadataService) =>
        {
            var publishers = await metadataService.GetBookPublishers();
            logger.LogInformation("Returning {count} publishers", publishers.Count());
            return publishers;
        });

        group.MapGet("/platforms", async (IMetadataService metadataService) =>
        {
            var platforms = await metadataService.GetGamePlatforms();
            logger.LogInformation("Returning {count} platforms", platforms.Count());
            return platforms;
        });

        group.MapGet("/developers", async (IMetadataService metadataService) =>
        {
            var developers = await metadataService.GetGameDevelopers();
            logger.LogInformation("Returning {count} developers", developers.Count());
            return developers;
        });

        group.MapGet("/artists", async (IMetadataService metadataService) =>
        {
            var artists = await metadataService.GetMusicArtists();
            logger.LogInformation("Returning {count} artists", artists.Count());
            return artists;
        });

        group.MapGet("/labels", async (IMetadataService metadataService) =>
        {
            var labels = await metadataService.GetMusicLabels();
            logger.LogInformation("Returning {count} labels", labels.Count());
            return labels;
        });

        return group;
    }
}
