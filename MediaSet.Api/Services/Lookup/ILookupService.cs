namespace MediaSet.Api.Services.Lookup;

public interface ILookupService
{
    Task<object?> LookupAsync(string entityType, string identifierType, string identifierValue, CancellationToken cancellationToken = default);
}
