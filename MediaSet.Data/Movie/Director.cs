using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Director : EntityAbstract
    {
        public ICollection<MovieDirector> MovieDirectors { get; set; }
    }
}
