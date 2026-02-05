using MediaSet.Api.Models;
using MediaSet.Api.Infrastructure.Storage;

namespace MediaSet.Api.Features.Entities.Models;

public interface IEntity
{
    public string? Id { get; set; }
    public MediaTypes Type { get; set; }
    public string Title { get; set; }
    public string Format { get; set; }
    public string? ImageUrl { get; set; }
    public Image? CoverImage { get; set; }
    public ImageLookup? ImageLookup { get; set; }
    public bool IsEmpty();
}
