using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Writer : EntityAbstract
    {
        public ICollection<MovieWriter> MovieWriters { get; set; } 
    }
}
