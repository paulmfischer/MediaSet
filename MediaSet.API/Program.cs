using MediaSet.Api.BookApi;
using Microsoft.EntityFrameworkCore;

var folder = Environment.SpecialFolder.LocalApplicationData;
var path = Environment.GetFolderPath(folder);
var dbPath = Path.Join(path, "mediaset.db");
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<MediaSetDbContext>(opt => opt.UseSqlite($"Data Source={dbPath}"));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapBooks();

app.Run();
