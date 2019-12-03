using System.Collections.Generic;

namespace MediaSet.Data
{
    public class Genre : EntityAbstract
    {
        public int MediaTypeId { get; set; }
        public ICollection<MediaGenre> MediaGenres { get; set; }
    }
}
