using Microsoft.EntityFrameworkCore;
using API.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<MediaSetContext>(opt => {
    opt.UseSqlite("Data Source=MediaSet.db");
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IEntityService<Movie>, MovieService>();
builder.Services.AddScoped<MetadataService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // initialize the db
    var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
    using (var scope = scopeFactory.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MediaSetContext>();
        if (db.Movies.Any())
        {
            SeedData.Cleanup(db);
        }

        SeedData.Initialize(db);
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
