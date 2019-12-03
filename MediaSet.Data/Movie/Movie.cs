using System;
using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Movie : IEntity
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public Media Media { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string SortTitle { get; set; }
        public string SubTitle { get; set; }
        public int Runtime { get; set; }
        public string Plot { get; set; }
        public string IMDBLink { get; set; }
        public ICollection<MovieStudio> MovieStudios { get; set; }
    }
}
