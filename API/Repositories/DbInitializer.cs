using Entities;

namespace Repositories;

public static class DbInitializer
{
    public static void Initialize(MediaSetContext context)
    {
        context.Database.EnsureCreated();

        if (context.Movies.Any())
        {
            return;   // DB has been seeded
        }

        context.Movies.AddRange(new MovieEntity[]
        {
            new MovieEntity { ISBN = "123", UPC = "321", IsTvSeries = false, Plot = "This is a plot", ReleaseDate = new DateTime(), Runtime = 140, SortTitle = "Title This", Title = "Title This" },
            new MovieEntity { ISBN = "456", UPC = "654", IsTvSeries = false, Plot = "Another plot", ReleaseDate = new DateTime(), Runtime = 110, SortTitle = "Hahaha", Title = "Hahahaha" },
            new MovieEntity { ISBN = "789", UPC = "987", IsTvSeries = false, Plot = "We made it", ReleaseDate = new DateTime(), Runtime = 124, SortTitle = "Good Stuff", Title = "The Good Stuff" },
        });
        context.SaveChanges();
    }
}