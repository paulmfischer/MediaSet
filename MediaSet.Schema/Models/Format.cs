namespace MediaSet.Data.Models
{
    public class Format : IEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public MediaType MediaType { get; set; }
    }
}
