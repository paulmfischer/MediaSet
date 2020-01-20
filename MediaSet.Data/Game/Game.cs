

namespace MediaSet.Data.GameData
{
    public class Game : IEntity
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public Media Media { get; set; }
        public string Barcode { get; set; }
        public Platform Platform { get; set; }
        public int PlatformId { get; set; }
        public string SubTitle { get; set; }
        public string ReleaseDate { get; set; }
        public Publisher Publisher { get; set; }
        public int? PublisherId { get; set; }
        public Developer Developer { get; set; }
        public int? DeveloperId { get; set; }
    }
}
