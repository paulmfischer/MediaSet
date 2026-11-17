using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediaSet.Api.Shared.Attributes;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MediaSet.Api.Shared.Models;

public class Music : IEntity
{
    public Music()
    {
        Type = MediaTypes.Musics;
    }

    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonIgnore]
    public MediaTypes Type { get; set; }

    [Required]
    [LookupProperty]
    [LookupIdentifier(2, IdentifierType.Entity)]
    public string Title { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    [Required]
    [LookupProperty]
    public string Artist { get; set; } = string.Empty;

    [Upload(HeaderName = "Release Date")]
    public string ReleaseDate { get; set; } = string.Empty;

    [Upload(HeaderName = "Genre")]
    public List<string> Genres { get; set; } = [];

    public int? Duration { get; set; }

    public string Label { get; set; } = string.Empty;

    [LookupIdentifier(1, IdentifierType.Upc)]
    public string Barcode { get; set; } = string.Empty;

    public int? Tracks { get; set; }
    public int? Discs { get; set; }
    public List<Disc> DiscList { get; set; } = [];

    /// <summary>
    /// Optional MusicBrainz release-group id written as <c>mbid:&lt;id&gt;</c>. Absent on older documents.
    /// </summary>
    public string? ExternalId { get; set; }

    public string? ImageUrl { get; set; }

    public Image? CoverImage { get; set; }

    public ImageLookup? ImageLookup { get; set; }

    /// <summary>
    /// Stored Plex data (the album's Plex summary on the paged search, the full Plex album data on the single lookup; typed object so
    /// Shared does not depend on the Plex feature), filled only when the album is linked to Plex. Never stored on the album and
    /// ignored on input, so edits never touch the Plex data.
    /// </summary>
    [BsonIgnore]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Plex { get; set; }

    public bool IsEmpty()
    {
        return string.IsNullOrWhiteSpace(Title)
          && string.IsNullOrWhiteSpace(Format)
          && string.IsNullOrWhiteSpace(Artist)
          && string.IsNullOrWhiteSpace(ReleaseDate)
          && Genres.Count == 0
          && !Duration.HasValue
          && string.IsNullOrWhiteSpace(Label)
          && string.IsNullOrWhiteSpace(Barcode)
          && !Tracks.HasValue
          && string.IsNullOrWhiteSpace(ExternalId)
          && !Discs.HasValue
          && DiscList.Count == 0
          && CoverImage == null;
    }
}

public class Disc
{
    public int TrackNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public int? Duration { get; set; }
}
