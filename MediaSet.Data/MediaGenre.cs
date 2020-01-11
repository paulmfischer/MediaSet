
namespace MediaSet.Data
{
    public class MediaGenre
    {
        public int MediaId { get; set; }
        public Media Media { get; set; }
        public int GenreId { get; set; }
        public Genre Genre { get; set; }
    }
}
