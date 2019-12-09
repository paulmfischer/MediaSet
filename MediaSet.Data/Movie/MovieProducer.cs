namespace MediaSet.Data.MovieData
{
    public class MovieProducer
    {
        public int MovieId { get; set; }
        public Movie Movie { get; set; }
        public int ProducerId { get; set; }
        public Producer Producer { get; set; }
    }
}
