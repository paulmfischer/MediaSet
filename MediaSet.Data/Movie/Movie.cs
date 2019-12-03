using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data.Movie
{
    public class Movie
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public DateTime ReleaseDate { get; set; }
        public string SortTitle { get; set; }
        public string SubTitle { get; set; }
        public int Runtime { get; set; }
        public string Plot { get; set; }
        public string IMDBLink { get; set; }
    }
}
