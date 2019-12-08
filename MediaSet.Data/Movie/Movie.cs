using System;
using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Movie : IEntity
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public Media Media { get; set; }
        public string ReleaseDate { get; set; }
        public string Runtime { get; set; }
        public string Plot { get; set; }
        public string IMDBLink { get; set; }
        public ICollection<MovieStudio> MovieStudios { get; set; }
    }
}
