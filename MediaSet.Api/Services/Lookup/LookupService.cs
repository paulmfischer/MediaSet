namespace MediaSet.Api.Services.Lookup;

public class LookupService : ILookupService
{
    private readonly IEnumerable<ILookupStrategy> _strategies;
    private readonly ILogger<LookupService> _logger;

    public LookupService(IEnumerable<ILookupStrategy> strategies, ILogger<LookupService> logger)
    {
        _strategies = strategies;
        _logger = logger;
    }

    public async Task<object?> LookupAsync(string entityType, string identifierType, string identifierValue, CancellationToken cancellationToken = default)
    {
        var strategy = _strategies.FirstOrDefault(s =>
            s.EntityType.Equals(entityType, StringComparison.OrdinalIgnoreCase) &&
            s.SupportsIdentifierType(identifierType));

        if (strategy == null)
        {
            _logger.LogWarning("No lookup strategy found for entityType {entityType} and identifierType {identifierType}", entityType, identifierType);
            return null;
        }

        _logger.LogInformation("Using {strategyType} for lookup of {entityType} by {identifierType}", strategy.GetType().Name, entityType, identifierType);

        // Use reflection to call the generic LookupAsync method
        var lookupMethod = strategy.GetType().GetMethod("LookupAsync");
        if (lookupMethod == null)
        {
            _logger.LogError("LookupAsync method not found on strategy {strategyType}", strategy.GetType().Name);
            return null;
        }

        var task = lookupMethod.Invoke(strategy, [identifierType, identifierValue, cancellationToken]) as Task;
        if (task == null)
        {
            _logger.LogError("Failed to invoke LookupAsync on strategy {strategyType}", strategy.GetType().Name);
            return null;
        }

        await task.ConfigureAwait(false);

        var resultProperty = task.GetType().GetProperty("Result");
        return resultProperty?.GetValue(task);
    }
}
