using MediaSet.Api.Features.Plex.Models;
using MediaSet.Api.Infrastructure.Plex;

namespace MediaSet.Api.Features.Plex.Services;

public interface IPlexSearchService
{
    /// <summary>Searches the libraries configured for the entity type for titles containing the query.</summary>
    Task<IReadOnlyList<PlexSearchResultDto>> SearchAsync(string entityType, string query, CancellationToken cancellationToken);
}

public class PlexSearchService(IPlexClient client) : IPlexSearchService
{
    public async Task<IReadOnlyList<PlexSearchResultDto>> SearchAsync(string entityType, string query, CancellationToken cancellationToken)
    {
        var itemType = PlexEntityTypes.LibraryType(entityType);
        var results = new List<PlexSearchResultDto>();
        foreach (var library in await client.GetLibrariesForEntityTypeAsync(entityType, cancellationToken))
        {
            var items = await client.GetItemsAsync(library, query, cancellationToken);
            // Search is scoped to the entity type: movies never return shows
            results.AddRange(items
                .Where(i => string.Equals(i.Type, itemType, StringComparison.OrdinalIgnoreCase))
                .Select(i => new PlexSearchResultDto(i.Title, i.RatingKey, i.Year, i.LibraryTitle, i.Guids)));
        }

        return results;
    }
}
