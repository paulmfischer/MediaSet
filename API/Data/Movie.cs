namespace API.Data;

public class Movie
{
    public int Id { get; set; }
    public string ReleaseDate { get; set; } = default!;
    public int Runtime { get; set; }
    public bool IsTvSeries { get; set; }
    public string Plot { get; set; } = default!;

    public MediaItem MediaItem { get; set; } = default!;
    public List<Genre> Genres { get; set; } = new List<Genre>();
    public Studio Studio { get; set; } = default!;
}