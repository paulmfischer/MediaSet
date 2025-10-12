using MediaSet.Api.Bindings;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace MediaSet.Api.Metadata;

internal static class MetadatApi
{
  public static RouteGroupBuilder MapMetadata(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/metadata");

    group.WithTags("Metadata");

    group.MapGet("/formats/{media}", async (MetadataService metadataService, [FromRoute] Parameter<MediaTypes> media) =>
    {
      MediaTypes mediaTypes = media;
      return mediaTypes switch
      {
        MediaTypes.Books => await metadataService.GetBookFormats(),
        MediaTypes.Movies => await metadataService.GetMovieFormats(),
        _ => throw new ArgumentException($"Media Type of {mediaTypes} is not supported")
      };
    });

    group.MapGet("/genres/{media}", async (MetadataService metadataService, [FromRoute] Parameter<MediaTypes> media) =>
    {
      MediaTypes mediaTypes = media;
      return mediaTypes switch
      {
        MediaTypes.Books => await metadataService.GetBookGenres(),
        MediaTypes.Movies => await metadataService.GetMovieGenres(),
        _ => throw new ArgumentException($"Media Type of {mediaTypes} is not supported")
      };
    });

    group.MapGet("/studios", async (MetadataService metadataService) => await metadataService.GetMovieStudios());
    group.MapGet("/authors", async (MetadataService metadataService) => await metadataService.GetBookAuthors());
    group.MapGet("/publishers", async (MetadataService metadataService) => await metadataService.GetBookPublishers());

    return group;
  }
}
