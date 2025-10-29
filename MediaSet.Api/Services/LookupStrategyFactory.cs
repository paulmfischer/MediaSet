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
}
