using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Shared.Models;

namespace MediaSet.Api.Infrastructure.Lookup.Strategies;

public class LookupStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public LookupStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ILookupStrategy<TResponse> GetStrategy<TResponse>(MediaTypes mediaType, IdentifierType identifierType)
        where TResponse : class
    {
        var strategies = _serviceProvider.GetServices<ILookupStrategy<TResponse>>();

        foreach (var strategy in strategies)
        {
            if (strategy.CanHandle(mediaType, identifierType))
            {
                return strategy;
            }
        }

        throw new NotSupportedException($"No strategy found for {mediaType} with identifier type: {identifierType}");
    }

    public ILookupStrategyBase GetStrategyBase(MediaTypes mediaType)
    {
        // Use BookResponse as a representative type to get any registered strategy for the media type
        return mediaType switch
        {
            MediaTypes.Books => GetStrategyBase<BookResponse>(mediaType),
            MediaTypes.Movies => GetStrategyBase<MovieResponse>(mediaType),
            MediaTypes.Games => GetStrategyBase<GameResponse>(mediaType),
            MediaTypes.Musics => GetStrategyBase<MusicResponse>(mediaType),
            _ => throw new NotSupportedException($"No strategy found for {mediaType}")
        };
    }

    private ILookupStrategyBase GetStrategyBase<TResponse>(MediaTypes mediaType)
        where TResponse : class
    {
        var strategies = _serviceProvider.GetServices<ILookupStrategy<TResponse>>();

        foreach (var strategy in strategies)
        {
            if (strategy.CanHandle(mediaType, IdentifierType.Entity))
            {
                return strategy;
            }
        }

        throw new NotSupportedException($"No strategy found for {mediaType} with identifier type: Entity");
    }
}
