namespace API.Data;

public static class SeedData
{
    public static void Initialize(MediaSetContext db)
    {
        var formats = new Format[]
        {
            new Format { Name = "DVD", MediaType = MediaType.Movie },
            new Format { Name = "Blu-Ray", MediaType = MediaType.Movie },
            new Format { Name = "Paperback", MediaType = MediaType.Book },
            new Format { Name = "GD-ROM", MediaType = MediaType.Game },
        };

        var genres = new Genre[]
        {
            new Genre { Name = "Comedy" },
            new Genre { Name = "Drama" },
        };

        var studios = new Studio[]
        {
            new Studio { Name = "Warner Brothers" },
            new Studio { Name = "Pixar" },
        };

        db.Formats.AddRange(formats);
        db.Genres.AddRange(genres);
        db.Studios.AddRange(studios);
        db.SaveChanges();

        var movies = new Movie[]
        {
            new Movie
            {
                Genres = new List<Genre> { genres.ElementAt(0) },
                IsTvSeries = false,
                Plot = "This is the plot of this!",
                ReleaseDate = "2004",
                Runtime = 115,
                Studio = studios.ElementAt(0),
                MediaItem = new MediaItem {
                    Barcode = "1234567890",
                    Format = formats.ElementAt(1),
                    Title = "My title"
                }
            }
        };

        db.Movies.AddRange(movies);
        db.SaveChanges();
    }
}