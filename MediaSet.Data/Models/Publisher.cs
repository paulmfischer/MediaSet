namespace MediaSet.Data.Models
{
    public class Publisher : IEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public MediaType MediaType { get; set; }
    }
}