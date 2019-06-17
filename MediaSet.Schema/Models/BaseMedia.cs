namespace MediaSet.Data.Models
{
    public class BaseMedia
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public Media Media { get; set; }
    }
}
