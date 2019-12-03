namespace MediaSet.Data
{
    public class Format : EntityAbstract
    {
        //public int Id { get; set; }
        //public string Name { get; set; }
        public int MediaTypeId { get; set; }
        public Media Media { get; set; }
    }
}
