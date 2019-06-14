namespace MediaSet.Schema.Models
{
    public class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public MediaType MediaType { get; set; }
    }
}