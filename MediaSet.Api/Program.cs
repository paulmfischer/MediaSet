using MediaSet.Api.Books;
using MediaSet.Api.Metadata;
using MediaSet.Api.Models;
using MediaSet.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure database settings
builder.Services.Configure<MediaSetDatabaseSettings>(
  builder.Configuration.GetSection("MediaSetDatabase")
);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<BookService>();
builder.Services.AddSingleton<MetadataService>();
builder.Services.AddSingleton<StatsService>();

var app = builder.Build();

// turn on swagger for all environments for now
// Configure the HTTP request pipeline.
// if (app.Environment.IsDevelopment())
// {
    app.UseSwagger();
    app.UseSwaggerUI();
// }

app.UseHttpsRedirection();

app.MapBooks();
app.MapMetadata();
app.MapStats();

app.Run();
