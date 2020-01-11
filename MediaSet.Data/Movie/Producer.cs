using Newtonsoft.Json;
using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Producer : EntityAbstract
    {
        [JsonIgnore]
        public ICollection<MovieProducer> MovieProducers { get; set; }
    }
}
