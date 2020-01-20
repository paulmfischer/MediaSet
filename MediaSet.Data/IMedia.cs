namespace MediaSet.Data
{
    public interface IMedia : IEntity
    {
        int MediaId { get; set; }
        Media Media { get; set; }
    }
}
