using System.Collections.Generic;

namespace MediaSet.Data
{
    public class MediaType : EntityAbstract
    {
        public ICollection<Genre> Genres { get; set; }
    }
}
