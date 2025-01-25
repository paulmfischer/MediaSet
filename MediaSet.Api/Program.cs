using MediaSet.Api.Books;
using MediaSet.Api.Metadata;
using MediaSet.Api.Models;
using MediaSet.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var logger = LoggerFactory.Create(config =>
{
  config.AddConsole();
}).CreateLogger("MediaSet.Api");

// Configure database settings
builder.Services.Configure<MediaSetDatabaseSettings>(
  builder.Configuration.GetSection("MediaSetDatabase")
);
builder.Services.AddSingleton<DatabaseService>();

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

app.Run();
