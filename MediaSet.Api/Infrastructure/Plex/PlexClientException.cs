namespace MediaSet.Api.Infrastructure.Plex;

/// <summary>
/// A Plex request failed. The message is fixed text plus the status code. It never carries the
/// request URL, the token or inner exception text.
/// </summary>
public sealed class PlexClientException : Exception
{
    public int? StatusCode { get; }

    public PlexClientException(int? statusCode, string reason)
        : base(statusCode.HasValue ? $"{reason} (status code {statusCode})." : $"{reason}.")
    {
        StatusCode = statusCode;
    }
}
