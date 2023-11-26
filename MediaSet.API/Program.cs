using MediaSet.Api.BookApi;
using MediaSet.Api.Metadata;
using MediaSet.Data.Entities;
using MediaSet.Data.Repositories;
using Microsoft.EntityFrameworkCore;

var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
var dbPath = Path.Join(path, "mediaset.db");
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MediaSetDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IMetadataRepository, MetadataRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapEntity<Format>("/formats");
app.MapEntity<Genre>("/genres");
app.MapEntity<Publisher>("/publisher");
app.MapBooks();

app.Run();
