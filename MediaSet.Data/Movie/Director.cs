using Newtonsoft.Json;
using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Director : EntityAbstract
    {
        [JsonIgnore]
        public ICollection<MovieDirector> MovieDirectors { get; set; }
    }
}
