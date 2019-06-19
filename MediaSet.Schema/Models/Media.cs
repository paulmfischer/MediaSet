using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MediaSet.Data.Models
{
    public class Media : IEntity
    {
        public int Id { get; set; }
        public MediaType MediaType { get; set; }

        [Required]
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string BarCode { get; set; }
        public string ISBN { get; set; }
        public Format Format { get; set; }
        public int? FormatId { get; set; }
        public virtual ICollection<MediaGenre> MediaGenres { get; set; }
    }

    public enum MediaType
    {
        Book = 1,
        Movie = 2,
        Game = 3
    }
}
