using System.Collections.Generic;

namespace MediaSet.Data.Models
{
    public class Genre : IEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public MediaType MediaType { get; set; }
        public virtual ICollection<MediaGenre> MediaGenres { get; set; }
    }
}