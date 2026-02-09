

namespace MediaSet.Api.Shared.Models;

public interface IEntity
{
    string? Id { get; set; }
    MediaTypes Type { get; set; }
    string Title { get; set; }
    string Format { get; set; }
    string? ImageUrl { get; set; }
    Image? CoverImage { get; set; }
    ImageLookup? ImageLookup { get; set; }
    bool IsEmpty();
}
