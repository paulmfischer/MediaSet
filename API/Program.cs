using Repositories;
using Services;
using Services.UPC.Upcitemdb;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<MediaSetContext>();

builder.Services.SetupUPCServices(builder.Configuration);

builder.Services.AddScoped<IMovieService, MovieService>();
builder.Services.AddScoped<IMovieRepository, MovieRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.CreateDbIfNonExisting();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
