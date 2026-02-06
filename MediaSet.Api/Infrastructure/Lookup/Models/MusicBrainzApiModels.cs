using System.Text.Json.Serialization;

namespace MediaSet.Api.Infrastructure.Lookup.Models;

public class MusicBrainzSearchResponse
{
    [JsonPropertyName("releases")]
    public List<MusicBrainzRelease> Releases { get; set; } = [];
}

public class MusicBrainzRelease
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("barcode")]
    public string Barcode { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("artist-credit")]
    public List<MusicBrainzArtistCredit> ArtistCredit { get; set; } = [];

    [JsonPropertyName("label-info")]
    public List<MusicBrainzLabelInfo> LabelInfo { get; set; } = [];

    [JsonPropertyName("media")]
    public List<MusicBrainzMedia> Media { get; set; } = [];

    [JsonPropertyName("tags")]
    public List<MusicBrainzTag> Tags { get; set; } = [];
}

public class MusicBrainzArtistCredit
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("artist")]
    public MusicBrainzArtist? Artist { get; set; }
}

public class MusicBrainzArtist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("sort-name")]
    public string SortName { get; set; } = string.Empty;
}

public class MusicBrainzLabelInfo
{
    [JsonPropertyName("label")]
    public MusicBrainzLabel? Label { get; set; }
}

public class MusicBrainzLabel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class MusicBrainzMedia
{
    [JsonPropertyName("format")]
    public string Format { get; set; } = string.Empty;

    [JsonPropertyName("track-count")]
    public int TrackCount { get; set; }

    [JsonPropertyName("tracks")]
    public List<MusicBrainzTrack> Tracks { get; set; } = [];
}

public class MusicBrainzTrack
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("length")]
    public int? Length { get; set; }

    [JsonPropertyName("recording")]
    public MusicBrainzRecording? Recording { get; set; }
}

public class MusicBrainzRecording
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("length")]
    public int? Length { get; set; }
}

public class MusicBrainzTag
{
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
