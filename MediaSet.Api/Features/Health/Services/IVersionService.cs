namespace MediaSet.Api.Features.Health.Services;

/// <summary>
/// Service for retrieving application version information.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the semantic version of the application.
    /// </summary>
    string GetVersion();

    /// <summary>
    /// Gets the commit SHA of the build.
    /// </summary>
    string GetCommitSha();

    /// <summary>
    /// Gets the build timestamp in UTC.
    /// </summary>
    string GetBuildTime();
}
