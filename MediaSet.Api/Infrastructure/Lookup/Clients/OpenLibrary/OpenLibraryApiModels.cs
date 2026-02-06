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
