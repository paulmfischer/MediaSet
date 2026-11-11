namespace MediaSet.Api.Shared.Models;

/// <summary>Restricts a paged search to the entities whose id is in (or, with Exclude, is not in) a set of ids.</summary>
public record IdFilter(IReadOnlyCollection<string> Ids, bool Exclude = false);
