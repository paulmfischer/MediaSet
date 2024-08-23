using MediaSet.Api.Models;
using MediaSet.Api.Services;

namespace MediaSet.Api.Metadata;

internal static class MetadatApi
{
  public static RouteGroupBuilder MapMetadata(this IEndpointRouteBuilder routes)
  {
    var group = routes.MapGroup("/metadata");

    group.WithTags("Metadata");

    group.MapGet("/formats/{mediaType}", async (MetadataService metadataService, MediaTypes mediaType) => {
      return mediaType switch
      {
        MediaTypes.Book => await metadataService.GetBookFormats(),
        _ => throw new ArgumentException($"Media Type of {mediaType} is not supported")
      };
    });

    group.MapGet("/authors", async (MetadataService metadataService) => await metadataService.GetBookAuthors());
    group.MapGet("/publishers", async (MetadataService metadataService) => await metadataService.GetBookPublishers());
    group.MapGet("/genres", async (MetadataService metadataService) => await metadataService.GetBookGenres());

    return group;
  }
}