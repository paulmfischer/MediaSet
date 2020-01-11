using MediaSet.Data.BookData;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace MediaSet.Data
{
    public class Publisher : EntityAbstract
    {
        public int MediaTypeId { get; set; }
        
        [JsonIgnore]
        public ICollection<BookPublisher> BookPublishers { get; set; }
    }
}
