namespace MediaSet.Data.MovieData
{
    public class MovieStudio
    {
        public int MovieId { get; set; }
        public Movie Movie { get; set; }
        public int StudioId { get; set; }
        public Studio Studio { get; set; }
    }
}
