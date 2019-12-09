using System.Collections.Generic;

namespace MediaSet.Data.MovieData
{
    public class Producer : EntityAbstract
    {
        public ICollection<MovieProducer> MovieProducers { get; set; }
    }
}
