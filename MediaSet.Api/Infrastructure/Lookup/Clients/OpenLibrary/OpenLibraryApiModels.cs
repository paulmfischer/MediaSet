using System.Text.Json.Serialization;

namespace MediaSet.Api.Infrastructure.Lookup.Clients.OpenLibrary;

public record ReadApiResponse(
  List<ReadApiItem> Items,
  Dictionary<string, ReadApiRecord> Records
);

public record ReadApiItem(
  string Match,
  string Status,
  string ItemUrl,
  ReadApiCover? Cover,
  string FromRecord,
  string PublishDate,
  string OlEditionId,
  string OlWorkId
);

public record ReadApiCover(
  string Small,
  string Medium,
  string Large
);
public record OpenLibrarySearchResponse(
    [property: JsonPropertyName("numFound")] int NumFound,
    [property: JsonPropertyName("docs")] List<OpenLibrarySearchDoc> Docs
);

public record OpenLibrarySearchDoc(
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("author_name")] List<string>? AuthorName,
    [property: JsonPropertyName("first_publish_year")] int? FirstPublishYear,
    [property: JsonPropertyName("isbn")] List<string>? Isbn,
    [property: JsonPropertyName("publisher")] List<string>? Publisher,
    [property: JsonPropertyName("subject")] List<string>? Subject,
    [property: JsonPropertyName("number_of_pages_median")] int? NumberOfPagesMedian,
    [property: JsonPropertyName("cover_i")] long? CoverId,
    [property: JsonPropertyName("cover_edition_key")] string? CoverEditionKey
);

public record ReadApiRecord(
  List<string> Isbns,
  List<string> Lccns,
  List<string> Oclcs,
  List<string> Olids,
  List<string> PublishDates,
  string RecordUrl,
  Dictionary<string, object>? Data,
  Dictionary<string, object>? Details
);
