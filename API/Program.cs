using Microsoft.EntityFrameworkCore;
using API;

var builder = WebApplication.CreateBuilder(args);

// Configure database
builder.Services.AddDbContext<MediaSetContext>(opt => {
    opt.UseSqlite("Data Source=MediaSet.db");
});

// Configure Open API
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    app.Map("/", () => Results.Redirect("/swagger"));
}

app.MapMetadata();
app.MapMovie();

app.Run();
