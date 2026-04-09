namespace MediaSet.Api.Shared.Models;

public record PagedResult<T>(
    IEnumerable<T> Items,
    long TotalCount,
    int Page,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
