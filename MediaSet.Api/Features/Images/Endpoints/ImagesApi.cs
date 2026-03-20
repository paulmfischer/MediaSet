using MediaSet.Api.Features.Images.Services;

namespace MediaSet.Api.Features.Images.Endpoints;

internal static class ImagesApi
{
    public static RouteGroupBuilder MapImages(this IEndpointRouteBuilder routes)
    {
        var logger = ((WebApplication)routes).Logger;
        var group = routes.MapGroup("/images");

        group.WithTags("Images");

        group.MapDelete("/orphaned", async (IImageManagementService imageManagementService, CancellationToken cancellationToken) =>
        {
            logger.LogInformation("Deleting orphaned images");
            var count = await imageManagementService.DeleteOrphanedImagesAsync(cancellationToken);
            return Results.Ok(new { deleted = count });
        });

        return group;
    }
}
