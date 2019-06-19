namespace MediaSet.Data.Models
{
    public class BaseMedia : IEntity
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public Media Media { get; set; }
    }
}
