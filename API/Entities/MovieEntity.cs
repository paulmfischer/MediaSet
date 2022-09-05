namespace Entities;

public class MovieEntity : IEntity
{
    public int Id { get; set; }
    public string ISBN { get; set; }
    public string UPC { get; set; }

    public string Title { get; set; }
    public string SortTitle { get; set; }
    public string Plot { get; set; }
    public DateTime ReleaseDate { get; set; }
    public int Runtime { get; set; }
    public bool IsTvSeries { get; set; }
}