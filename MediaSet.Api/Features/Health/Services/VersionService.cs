using System.Reflection;

namespace MediaSet.Api.Features.Health.Services;

/// <summary>
/// Implementation of version service that reads version information from assembly metadata.
/// </summary>
public class VersionService : IVersionService
{
    private readonly string _version;
    private readonly string _commitSha;
    private readonly string _buildTime;

    public VersionService()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0-local";
        
        // MinVer sets InformationalVersion in the format: version+commitSha
        var parts = informationalVersion.Split('+');
        _version = parts[0];
        _commitSha = parts.Length > 1 ? parts[1] : "unknown";
        
        // Use the build time from the assembly (or current time as fallback)
        var buildTimeAttr = assembly.GetCustomAttribute<AssemblyMetadataAttribute>();
        _buildTime = buildTimeAttr?.Value ?? DateTime.UtcNow.ToString("o");
    }

    public string GetVersion() => _version;

    public string GetCommitSha() => _commitSha;

    public string GetBuildTime() => _buildTime;
}
