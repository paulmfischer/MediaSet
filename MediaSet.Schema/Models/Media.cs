namespace MediaSet.Data.Models
{
    public class Media
    {
        public int Id { get; set; }
        public string ISBN { get; set; }
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string SubTitle { get; set; }
        public Format Format { get; set; }
        public int? FormatId { get; set; }
        public Genre Genre { get; set; }
        public int? GenreId { get; set; }
    }

    public enum MediaType
    {
        Book = 1,
        Movie = 2,
        Game = 3
    }
}
