namespace MediaSet.Api.Infrastructure.Lookup.Models;

public record BookResponse(
  string Title,
  string Subtitle,
  List<Author> Authors,
  int NumberOfPages,
  List<Publisher> Publishers,
  string PublishDate,
  List<Subject> Subjects,
  string? Format = null,
  string? ImageUrl = null,
  LookupProvenance? Provenance = null,
  IReadOnlyList<BookArtwork>? Artwork = null
);

public record BookArtwork(string Url, string Scope, string? EditionId = null);

public record Author(
  string Name,
  string Url
);

public record Publisher(string Name);

public record Subject(string Name, string Url);
