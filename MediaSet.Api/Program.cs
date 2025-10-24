using System.Diagnostics;
using System.Text.Json.Serialization;
using MediaSet.Api.Bindings;
using MediaSet.Api.Clients;
using MediaSet.Api.Entities;
using MediaSet.Api.Lookup;
using MediaSet.Api.Metadata;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Configure console logging with scopes and timestamps
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fff zzz ";
});
// Remove automatic TraceId/SpanId/ParentId printing from console logs
builder.Logging.Configure(options => options.ActivityTrackingOptions = ActivityTrackingOptions.None);

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

// conditionally register open library if the configuration exists
var openLibraryConfig = builder.Configuration.GetSection(nameof(OpenLibraryConfiguration));
if (openLibraryConfig.Exists())
{
    // Log using a bootstrap logger since app isn't built yet
    using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole());
    var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("MediaSet.Api");
    bootstrapLogger.LogInformation("OpenLibrary configuration exists. Setting up OpenLibrary services.");
    builder.Services.Configure<OpenLibraryConfiguration>(openLibraryConfig);
    builder.Services.AddHttpClient<IOpenLibraryClient, OpenLibraryClient>((serviceProvider, client) =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<OpenLibraryConfiguration>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.Timeout);
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("User-Agent", $"MediaSet/1.0 (${options.ContactEmail})");
    });
}

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen((setup) =>
{
    setup.SchemaFilter<ParameterSchemaFilter>();
});

// Built-in HTTP request/response logging
builder.Services.AddHttpLogging(logging =>
{
    logging.LoggingFields =
      HttpLoggingFields.RequestMethod |
      HttpLoggingFields.RequestPath |
      HttpLoggingFields.ResponseStatusCode |
      HttpLoggingFields.Duration;
    logging.RequestHeaders.Add("User-Agent");
    logging.RequestHeaders.Add("X-Request-ID");
    logging.RequestHeaders.Add("X-Correlation-ID");
    logging.ResponseHeaders.Add("X-Request-ID");
    logging.ResponseHeaders.Add("X-Correlation-ID");
});

builder.Services.AddScoped<IEntityService<Book>, EntityService<Book>>();
builder.Services.AddScoped<IEntityService<Movie>, EntityService<Movie>>();
builder.Services.AddScoped<IEntityService<Game>, EntityService<Game>>();
builder.Services.AddScoped<IEntityService<Music>, EntityService<Music>>();
builder.Services.AddScoped<IMetadataService, MetadataService>();
builder.Services.AddScoped<IStatsService, StatsService>();

var app = builder.Build();

// turn on swagger for all environments for now
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
app.UseSwagger();
app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

// Enable HTTP logging
app.UseHttpLogging();

// Correlation ID + request timing middleware (no TraceId)
app.Use(async (context, next) =>
{
    var logger = context.RequestServices
      .GetRequiredService<ILoggerFactory>()
      .CreateLogger("RequestTiming");

    // Get or generate correlation ID
    var correlationId = context.Request.Headers["X-Request-ID"].FirstOrDefault()
      ?? context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
      ?? Guid.NewGuid().ToString();

    // Add correlation ID to response headers
    context.Response.Headers["X-Request-ID"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId;

    var sw = Stopwatch.StartNew();

    // Use a formatted string scope containing just the CorrelationId
    using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
    {
        try
        {
            await next();
        }
        finally
        {
            sw.Stop();
            logger.LogDebug(
              "HTTP {method} {path} -> {status} in {elapsedMs} ms",
              context.Request.Method,
              context.Request.Path.Value,
              context.Response.StatusCode,
              sw.Elapsed.TotalMilliseconds
            );
        }
    }
});

// Health endpoint group
app.MapHealth();

app.MapEntity<Movie>();
app.MapEntity<Book>();
app.MapEntity<Game>();
app.MapEntity<Music>();
app.MapMetadata();
app.MapStats();

if (openLibraryConfig.Exists())
{
    app.MapIsbnLookup();
}

app.Run();

// Make the implicit Program class public so test projects can access it
public partial class Program { }
