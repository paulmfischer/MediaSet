using MediaSet.Api.Clients;
using MediaSet.Api.Models;

namespace MediaSet.Api.Services;

public class LookupStrategyFactory
{
    private readonly IServiceProvider _serviceProvider;

    public LookupStrategyFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ILookupStrategy<BookResponse> GetBookStrategy(IdentifierType identifierType)
    {
        var strategy = _serviceProvider.GetService<BookLookupStrategy>();
        if (strategy == null || !strategy.CanHandle(MediaTypes.Books, identifierType))
        {
            throw new NotSupportedException($"No strategy found for Books with identifier type: {identifierType}");
        }
        return strategy;
    }

    public ILookupStrategy<MovieResponse> GetMovieStrategy(IdentifierType identifierType)
    {
        var strategy = _serviceProvider.GetService<MovieLookupStrategy>();
        if (strategy == null || !strategy.CanHandle(MediaTypes.Movies, identifierType))
        {
            throw new NotSupportedException($"No strategy found for Movies with identifier type: {identifierType}");
        }
        return strategy;
    }
}
