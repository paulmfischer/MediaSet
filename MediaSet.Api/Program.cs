using System.Text.Json.Serialization;
using MediaSet.Api.Bindings;
using MediaSet.Api.Clients;
using MediaSet.Api.Entities;
using MediaSet.Api.Helpers;
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

// conditionally register open library lookup if the configuration exists
var isBookLookupEnabled = builder.AddOpenLibraryLookup(logger);

// conditionally register movie lookup if the configuration exists
var isMovieLookupEnabled = builder.AddMovieLookup(logger);

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
builder.Services.AddScoped<LookupService>();

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
app.MapLookupsApi(isBookLookupEnabled, isMovieLookupEnabled);

app.Run();
