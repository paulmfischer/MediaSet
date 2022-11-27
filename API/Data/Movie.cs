namespace API.Data;

public class Movie
{
    public int Id { get; set; }
    public string ReleaseDate { get; set; }
    public int Runtime { get; set; }
    public bool IsTvSeries { get; set; }
    public string Plot { get; set; }

    public MediaItem MediaItem { get; set; }
    public List<Genre> Genres { get; set; }
    public Studio Studio { get; set; }

    // public Movie(int id, string releaseDate, int runtime, bool isTvSeries, string plot, Media media)
    // {
    //     Id = id;
    //     ReleaseDate = releaseDate;
    //     Runtime = runtime;
    //     IsTvSeries = isTvSeries;
    //     Plot = plot;
    //     Media = media;
    // }
}