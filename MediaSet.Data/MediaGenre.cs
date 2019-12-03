using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data
{
    //[HasNoKey()]
    public class MediaGenre
    {
        public int MediaId { get; set; }
        public Media Media { get; set; }
        public int GenreId { get; set; }
        public Genre Genre { get; set; }
    }
}
