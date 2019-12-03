using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data
{
    public class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<MediaType> MediaTypes { get; set; }
        public ICollection<MediaGenre> MediaGenres { get; set; }
    }
}
