using Newtonsoft.Json;
using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Writer : EntityAbstract
    {
        [JsonIgnore]
        public ICollection<MovieWriter> MovieWriters { get; set; } 
    }
}
