namespace API.Data;

public static class SeedData
{
    public static void Initialize(MediaSetContext db)
    {
        var formats = new Format[]
        {
            new Format { Name = "DVD", MediaType = MediaType.Movie },
            new Format { Name = "Blu-Ray", MediaType = MediaType.Movie }
        };

        db.Formats.AddRange(formats);
        db.SaveChanges();
    }
}