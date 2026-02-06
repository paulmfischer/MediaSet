using MediaSet.Api.Infrastructure.Lookup.Models;
using MediaSet.Api.Features.Entities.Models;
using System.Text.Json.Serialization;
using MediaSet.Api.Features.Logs.Endpoints;
using MediaSet.Api.Features.Entities.Endpoints;
using MediaSet.Api.Shared.Extensions;
using MediaSet.Api.Features.Lookup.Endpoints;
using MediaSet.Api.Features.Config.Endpoints;
using MediaSet.Api.Features.Metadata.Endpoints; using MediaSet.Api.Features.Health.Endpoints; using MediaSet.Api.Features.Statistics.Endpoints;
using MediaSet.Api.Features.Images.Models;
using MediaSet.Api.Features.Images.Services;
using MediaSet.Api.Features.Metadata.Services;
using MediaSet.Api.Features.Statistics.Services;
using MediaSet.Api.Features.Statistics.Models;
using MediaSet.Api.Features.Health.Services;
using MediaSet.Api.Shared.Constraints;
using MediaSet.Api.Infrastructure.DataAccess;
using MediaSet.Api.Infrastructure.Database;
using MediaSet.Api.Infrastructure.Caching;
using MediaSet.Api.Infrastructure.Lookup.Strategies;
using MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary;
using MediaSet.Api.Infrastructure.Lookup.Clients.Tmdb;
using MediaSet.Api.Infrastructure.Lookup.Clients.GiantBomb;
using MediaSet.Api.Infrastructure.Lookup.Clients.MusicBrainz;
using MediaSet.Api.Infrastructure.Lookup.Clients.UpcItemDb;
using MediaSet.Api.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;

// Configure bootstrap logger for very early configuration
LoggingExtensions.ConfigureBootstrapLogger();
var bootstrapLogger = LoggingExtensions.GetBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Configure logging and tracing
builder.UseSerilogConfiguration()
       .ConfigureActivityTracking()
       .ConfigureHttpLogging()
       .ConfigureOpenTelemetry();

// configure enums as strings
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Configure database settings
builder.Services.Configure<MediaSetDatabaseSettings>(builder.Configuration.GetSection(nameof(MediaSetDatabaseSettings)));
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();

// Configure cache settings and services
builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection(nameof(CacheSettings)));
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();

// Configure image storage settings and services
builder.Services.Configure<ImageConfiguration>(builder.Configuration.GetSection(nameof(ImageConfiguration)));
var imageConfig = builder.Configuration.GetSection(nameof(ImageConfiguration)).Get<ImageConfiguration>();
if (imageConfig != null)
{
    bootstrapLogger.Information("Image storage configured with path: {StoragePath}", imageConfig.StoragePath);
    
    // Convert relative paths to absolute paths relative to the content root
    var storagePath = Path.IsPathRooted(imageConfig.StoragePath) 
        ? imageConfig.StoragePath 
        : Path.Combine(builder.Environment.ContentRootPath, imageConfig.StoragePath);
    
    // Ensure storage directory exists
    if (!Directory.Exists(storagePath))
    {
        Directory.CreateDirectory(storagePath);
        bootstrapLogger.Information("Created image storage directory: {StorageDirectory}", storagePath);
    }
    
    builder.Services.AddSingleton<IImageStorageProvider>(sp => 
        new LocalFileStorageProvider(
            storagePath, 
            sp.GetRequiredService<ILogger<LocalFileStorageProvider>>()));
    builder.Services.AddHttpClient<IImageService, ImageService>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<IOptions<ImageConfiguration>>().Value;
        client.Timeout = TimeSpan.FromSeconds(config.HttpTimeoutSeconds);
    });
}
var openLibraryConfig = builder.Configuration.GetSection(nameof(OpenLibraryConfiguration));
if (openLibraryConfig.Exists())
{
    bootstrapLogger.Information("OpenLibrary configuration exists. Setting up OpenLibrary services.");
    builder.Services.Configure<OpenLibraryConfiguration>(openLibraryConfig);
    builder.Services.AddHttpClient<IOpenLibraryClient, OpenLibraryClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<OpenLibraryConfiguration>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.Timeout);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"MediaSet/1.0 ({options.ContactEmail})");
    });
}

// Configure UpcItemDb client
var upcItemDbConfig = builder.Configuration.GetSection(nameof(UpcItemDbConfiguration));
if (upcItemDbConfig.Exists())
{
    bootstrapLogger.Information("UpcItemDb configuration exists. Setting up UpcItemDb services.");
    builder.Services.Configure<UpcItemDbConfiguration>(upcItemDbConfig);
    builder.Services.AddHttpClient<IUpcItemDbClient, UpcItemDbClient>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<IOptions<UpcItemDbConfiguration>>().Value;
        client.BaseAddress = new Uri(config.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(config.Timeout);
    });
}

// Configure TMDB client
var tmdbConfig = builder.Configuration.GetSection(nameof(TmdbConfiguration));
if (tmdbConfig.Exists())
{
    bootstrapLogger.Information("TMDB configuration exists. Setting up TMDB services.");
    builder.Services.Configure<TmdbConfiguration>(tmdbConfig);
    builder.Services.AddHttpClient<ITmdbClient, TmdbClient>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<IOptions<TmdbConfiguration>>().Value;
        client.BaseAddress = new Uri(config.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(config.Timeout);
        
        if (!string.IsNullOrEmpty(config.BearerToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {config.BearerToken}");
        }
    });
}

// Configure GiantBomb client
var giantBombConfig = builder.Configuration.GetSection(nameof(GiantBombConfiguration));
if (giantBombConfig.Exists())
{
    bootstrapLogger.Information("GiantBomb configuration exists. Setting up GiantBomb services.");
    builder.Services.Configure<GiantBombConfiguration>(giantBombConfig);
    builder.Services.AddHttpClient<IGiantBombClient, GiantBombClient>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<IOptions<GiantBombConfiguration>>().Value;
        client.BaseAddress = new Uri(config.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(config.Timeout);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "MediaSet/1.0 (GiantBomb)");
    });
}

// Configure MusicBrainz client
var musicBrainzConfig = builder.Configuration.GetSection(nameof(MusicBrainzConfiguration));
if (musicBrainzConfig.Exists())
{
    bootstrapLogger.Information("MusicBrainz configuration exists. Setting up MusicBrainz services.");
    builder.Services.Configure<MusicBrainzConfiguration>(musicBrainzConfig);
    builder.Services.AddHttpClient<IMusicBrainzClient, MusicBrainzClient>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<IOptions<MusicBrainzConfiguration>>().Value;
        client.BaseAddress = new Uri(config.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(config.Timeout);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", config.UserAgent);
    });
}

// Register lookup strategies and factory
if (openLibraryConfig.Exists() && upcItemDbConfig.Exists())
{
    builder.Services.AddScoped<ILookupStrategy<BookResponse>, BookLookupStrategy>();
}
if (upcItemDbConfig.Exists() && tmdbConfig.Exists())
{
    builder.Services.AddScoped<ILookupStrategy<MovieResponse>, MovieLookupStrategy>();
}
if (upcItemDbConfig.Exists() && giantBombConfig.Exists())
{
    builder.Services.AddScoped<ILookupStrategy<GameResponse>, GameLookupStrategy>();
}
if (musicBrainzConfig.Exists())
{
    builder.Services.AddScoped<ILookupStrategy<MusicResponse>, MusicLookupStrategy>();
}
if (openLibraryConfig.Exists() || tmdbConfig.Exists() || musicBrainzConfig.Exists())
{
    builder.Services.AddScoped<LookupStrategyFactory>();
}

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen((setup) =>
{
    setup.SchemaFilter<ParameterSchemaFilter>();
});

// Built-in HTTP request/response logging is configured via ConfigureHttpLogging()
builder.Services.AddScoped<HttpLoggingFilterMiddleware>();
builder.Services.AddScoped<TraceIdHeaderMiddleware>();

builder.Services.AddScoped<IEntityService<Book>, EntityService<Book>>();
builder.Services.AddScoped<IEntityService<Movie>, EntityService<Movie>>();
builder.Services.AddScoped<IEntityService<Game>, EntityService<Game>>();
builder.Services.AddScoped<IEntityService<Music>, EntityService<Music>>();
builder.Services.AddScoped<IMetadataService, MetadataService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddSingleton<IVersionService, VersionService>();

// Configure background image lookup service
builder.Services.Configure<BackgroundImageLookupConfiguration>(
    builder.Configuration.GetSection(nameof(BackgroundImageLookupConfiguration)));

var backgroundImageLookupConfig = builder.Configuration
    .GetSection(nameof(BackgroundImageLookupConfiguration))
    .Get<BackgroundImageLookupConfiguration>();

if (backgroundImageLookupConfig?.Enabled == true)
{
    // Skip validation in Development environment to allow flexible testing schedules
    string? errorMessage = null;
    var shouldRegister = builder.Environment.IsDevelopment() || backgroundImageLookupConfig.IsValid(out errorMessage);

    if (shouldRegister)
    {
        bootstrapLogger.Information(
            "Background image lookup service enabled with schedule: {Schedule} (Batch size: {BatchSize}, Max runtime: {MaxRuntime} minutes)",
            backgroundImageLookupConfig.Schedule,
            backgroundImageLookupConfig.BatchSize,
            backgroundImageLookupConfig.MaxRuntimeMinutes);
        builder.Services.AddScoped<IImageLookupService, ImageLookupService>();
        builder.Services.AddHostedService<BackgroundImageLookupService>();
    }
    else
    {
        bootstrapLogger.Warning(
            "Background image lookup service is enabled but has invalid configuration: {ErrorMessage}. Service will not start.",
            errorMessage);
    }
}
else
{
    bootstrapLogger.Information("Background image lookup service is disabled");
}

// Configure SerilogTracing to capture spans and write to Seq
using var listener = LoggingExtensions.ConfigureSerilogTracing();

var app = builder.Build();

app.UseHttpsRedirection();

// Configure logging middleware
// Set trace ID early, before any logging occurs (must be before Swagger and other middleware)
app.UseMiddleware<TraceIdHeaderMiddleware>();
app.UseHttpLoggingMiddleware();

// turn on swagger for all environments for now
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
// }

// Configure static file serving for images folder
if (imageConfig != null)
{
    var storagePath = Path.IsPathRooted(imageConfig.StoragePath)
        ? imageConfig.StoragePath
        : Path.Combine(builder.Environment.ContentRootPath, imageConfig.StoragePath);

    if (Directory.Exists(storagePath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storagePath),
            RequestPath = "/static/images",
            DefaultContentType = "application/octet-stream",
            ServeUnknownFileTypes = true,
            HttpsCompression = Microsoft.AspNetCore.Http.Features.HttpsCompressionMode.Compress,
            OnPrepareResponse = ctx =>
            {
                const int durationInSeconds = 604800; // 7 days
                ctx.Context.Response.Headers.CacheControl = $"public, max-age={durationInSeconds}";
                ctx.Context.Response.Headers.Expires = DateTimeOffset.UtcNow.AddSeconds(durationInSeconds).ToString("R");
            }
        });
    }
}

// Health endpoint group
app.MapHealth();

// Logs endpoint group for client-side log ingestion
app.MapLogs();

app.MapEntity<Movie>();
app.MapEntity<Book>();
app.MapEntity<Game>();
app.MapEntity<Music>();
app.MapMetadata();
app.MapStats();

if (openLibraryConfig.Exists() || tmdbConfig.Exists() || giantBombConfig.Exists())
{
    app.MapLookup();
}

// Map configuration endpoints
app.MapConfig();

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program { }
