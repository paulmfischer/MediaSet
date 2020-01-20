using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Movie : IMedia
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public Media Media { get; set; }
        public string Barcode { get; set; }
        public string ReleaseDate { get; set; }
        public string Runtime { get; set; }
        public string Plot { get; set; }
        public string IMDBLink { get; set; }
        public int? StudioId { get; set; }
        public Studio Studio { get; set; }
        public ICollection<MovieDirector> MovieDirectors { get; set; }
        public ICollection<MovieProducer> MovieProducers { get; set; }
        public ICollection<MovieWriter> MovieWriters { get; set; }
    }
}
