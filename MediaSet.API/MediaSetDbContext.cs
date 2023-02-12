using Microsoft.EntityFrameworkCore;
using MediaSet.API.Books;

namespace MediaSet.API;

public class MediaSetDbContext : DbContext
{
    public DbSet<Book> Books { get; set; }

    private string DatabasePath { get; }

    public MediaSetDbContext(DbContextOptions<MediaSetDbContext> options) : base(options) {}
}

public static class SeedDataExtension
{
    public static void UseDbSeeding(this IApplicationBuilder app)
    {
        var factory = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>();
        using var serviceScope = factory.CreateScope();
        var context = serviceScope.ServiceProvider.GetRequiredService<MediaSetDbContext>();

        if (!context.Books.Any())
        {
            // Seed here
            context.Books.Add(new()
            {
                Id = 1,
                ISBN = "1234567890",
                NumberOfPages = 150,
                PublishDate = new DateTime(),
                Title = "My Title",
            });

            context.SaveChanges();
        }
    }
}