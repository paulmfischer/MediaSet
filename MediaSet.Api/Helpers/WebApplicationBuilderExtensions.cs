using MediaSet.Api.Clients;
using Microsoft.Extensions.Options;

namespace MediaSet.Api.Helpers;

public static class WebApplicationBuilderExtensions
{
  public static bool AddOpenLibraryLookup(this WebApplicationBuilder builder, ILogger logger)
  {
    // conditionally register open library if the configuration exists
    var openLibraryConfig = builder.Configuration.GetSection(nameof(OpenLibraryConfiguration));
    if (openLibraryConfig.Exists())
    {
      logger.LogInformation("OpenLibrary Configuration is set, setting up Book lookup services");
      builder.Services.Configure<OpenLibraryConfiguration>(openLibraryConfig);
      builder.Services.AddHttpClient<OpenLibraryClient>((serviceProvider, client) =>
      {
          var options = serviceProvider.GetRequiredService<IOptions<OpenLibraryConfiguration>>().Value;
          client.BaseAddress = new Uri(options.BaseUrl);
          client.Timeout = TimeSpan.FromSeconds(options.Timeout);
          client.DefaultRequestHeaders.Add("Accept", "application/json");
          client.DefaultRequestHeaders.Add("User-Agent", $"MediaSet/1.0 (${options.ContactEmail})");
      });
    }
    else
    {
      logger.LogInformation("OpenLibrary Configuration is missing, will not setup Book lookup services");
    }
    
    return openLibraryConfig.Exists();
  }
  
  public static bool AddMovieLookup(this WebApplicationBuilder builder, ILogger logger)
  {
    var tmdbConfig = builder.Configuration.GetSection(nameof(TmdbClientConfiguration));
    var upcItemConfig = builder.Configuration.GetSection(nameof(UpcItemClientConfiguration));
    if (tmdbConfig.Exists())
    {
      logger.LogInformation("Tmdb and UpcItem Configuration is set, setting up Movie lookup services");
      builder.Services.Configure<TmdbClientConfiguration>(tmdbConfig);
      builder.Services.AddHttpClient<TmdbClient>((serviceProvider, client) =>
      {
        var options = serviceProvider.GetRequiredService<IOptions<TmdbClientConfiguration>>().Value;
        logger.LogInformation("TmdbClient setting base address: {baseAddress}", options.BaseUrl);
        client.BaseAddress = new Uri(options.BaseUrl);
      });
      
      builder.Services.Configure<UpcItemClientConfiguration>(upcItemConfig);
      builder.Services.AddHttpClient<UpcItemdbClient>((serviceProvider, client) =>
      {
        var options = serviceProvider.GetRequiredService<IOptions<UpcItemClientConfiguration>>().Value;
        logger.LogInformation("UpcItemClient setting base address: {baseAddress}", options.BaseUrl);
        client.BaseAddress = new Uri(options.BaseUrl);
      });
    }
    else
    {
      logger.LogInformation("Tmdb and UpcItem configuration are missing, will not setup Movie lookup services");
    }
    
    return tmdbConfig.Exists();
  }
}