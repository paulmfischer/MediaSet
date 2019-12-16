using MediaSet.Data.BookData;
using System.Collections.Generic;

namespace MediaSet.Data
{
    public class Publisher : EntityAbstract
    {
        public int MediaTypeId { get; set; }
        public ICollection<BookPublisher> BookPublishers { get; set; }
    }
}
