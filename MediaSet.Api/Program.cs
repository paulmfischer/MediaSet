using System.Text.Json.Serialization;
using MediaSet.Api.Bindings;
using MediaSet.Api.Clients;
using MediaSet.Api.Entities;
using MediaSet.Api.Lookup;
using MediaSet.Api.Metadata;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var logger = LoggerFactory.Create(config =>
{
  config.AddConsole();
}).CreateLogger("MediaSet.Api");

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
builder.Services.AddSingleton<DatabaseService>();

// conditionally register open library if the configuration exists
var openLibraryConfig = builder.Configuration.GetSection(nameof(OpenLibraryConfiguration));
if (openLibraryConfig.Exists())
{
  builder.Services.Configure<OpenLibraryConfiguration>(openLibraryConfig);
  builder.Services.AddHttpClient<OpenLibraryClient>((serviceProvider, client) => {
      var options = serviceProvider.GetRequiredService<IOptions<OpenLibraryConfiguration>>().Value;
      client.BaseAddress = new Uri(options.BaseUrl);
      client.Timeout = TimeSpan.FromSeconds(options.Timeout);
      client.DefaultRequestHeaders.Add("Accept", "application/json");
  });
}

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen((setup) => {
  setup.SchemaFilter<ParameterSchemaFilter>();
});

builder.Services.AddScoped<EntityService<Book>>();
builder.Services.AddScoped<EntityService<Movie>>();
builder.Services.AddScoped<MetadataService>();
builder.Services.AddScoped<StatsService>();

var app = builder.Build();

// turn on swagger for all environments for now
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

app.MapEntity<Movie>();
app.MapEntity<Book>();
app.MapMetadata();
app.MapStats();

if (openLibraryConfig.Exists())
{
  app.MapIsbnLookup();
}

app.Run();
