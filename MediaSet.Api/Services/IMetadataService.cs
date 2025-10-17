namespace MediaSet.Api.Services;

public interface IMetadataService
{
  Task<IEnumerable<string>> GetBookFormats();
  Task<IEnumerable<string>> GetBookAuthors();
  Task<IEnumerable<string>> GetBookPublishers();
  Task<IEnumerable<string>> GetBookGenres();
  Task<IEnumerable<string>> GetMovieFormats();
  Task<IEnumerable<string>> GetMovieStudios();
  Task<IEnumerable<string>> GetMovieGenres();
  Task<IEnumerable<string>> GetGameFormats();
  Task<IEnumerable<string>> GetGamePlatforms();
  Task<IEnumerable<string>> GetGameDevelopers();
  Task<IEnumerable<string>> GetGamePublishers();
  Task<IEnumerable<string>> GetGameGenres();
}
