using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Studio : EntityAbstract
    {
        public ICollection<MovieStudio> MovieStudios { get; set; }
    }
}
