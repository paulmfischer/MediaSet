namespace MediaSet.Api.Models;

public interface IEntity
{
    public string? Id { get; set; }
    public MediaTypes Type { get; set; }
    public string Title { get; set; }
    public string Format { get; set; }
    public string? ImageUrl { get; set; }
    public Image? CoverImage { get; set; }
    public bool IsEmpty();
}
