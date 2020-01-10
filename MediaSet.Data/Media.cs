using MediaSet.Data.BookData;
using MediaSet.Data.MovieData;
using MediaSet.Data.GameData;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MediaSet.Data
{
    public class Media
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int? FormatId { get; set; }
        public Format Format { get; set; }
        public int MediaTypeId { get; set; }
        public ICollection<MediaGenre> MediaGenres { get; set; }

        [JsonIgnore]
        public Movie Movie { get; set; }
        [JsonIgnore]
        public Book Book { get; set; }
        [JsonIgnore]
        public Game Game { get; set; }
    }
}
