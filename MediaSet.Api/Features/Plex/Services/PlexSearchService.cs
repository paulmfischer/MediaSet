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
    public const int MaxResults = 50;
    public const int MinQueryLength = 2;

    public async Task<IReadOnlyList<PlexSearchResultDto>> SearchAsync(string entityType, string query, CancellationToken cancellationToken)
    {
        var itemTypes = PlexEntityTypes.LibraryTypes(entityType);
        var results = new List<PlexSearchResultDto>();
        foreach (var library in await client.GetLibrariesForEntityTypeAsync(entityType, cancellationToken))
        {
            var items = await client.GetItemsAsync(library, query, false, cancellationToken);
            // Movies and shows (for TV series entries); other item types (episodes, seasons) are never returned
            results.AddRange(items
                .Where(i => itemTypes.Contains(i.Type, StringComparer.OrdinalIgnoreCase))
                .Select(i => new PlexSearchResultDto(i.Title, i.RatingKey, i.Year, i.LibraryTitle, i.Guids, i.Type.ToLowerInvariant())));
        }

        return results.Take(MaxResults).ToList();
    }
}
