using System.ComponentModel.DataAnnotations;

namespace API;

public class Movie
{
    public int Id { get; set; }
    [Required]
    public string Title { get; set; } = default!;
    public string? Barcode { get; set; }
    public string? ReleaseDate { get; set; }
    public int? Runtime { get; set; }
    public bool? IsTvSeries { get; set; }
    public string? Plot { get; set; }
    public ICollection<Genre> Genres { get; set; } = default!;
    public int? StudioId { get; set; }
    public Studio? Studio { get; set; }
    public int? FormatId { get; set; }
    public Format? Format { get; set; }
}

public static class MovieMappingExtensions
{
    public static Movie AsNewMovie(this Movie movie, List<Genre> dbGenres)
    {
        return new()
        {
            Title = movie.Title,
            Barcode = movie.Barcode,
            ReleaseDate = movie.ReleaseDate,
            Runtime = movie.Runtime,
            IsTvSeries = movie.IsTvSeries,
            Plot = movie.Plot,
            Genres = dbGenres,
            StudioId = movie.StudioId,
            FormatId = movie.FormatId,
        };
    }
}