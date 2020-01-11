using Newtonsoft.Json;
using System.Collections.Generic;

namespace MediaSet.Data.BookData
{
    public class Author : EntityAbstract
    {
        [JsonIgnore]
        public ICollection<BookAuthor> BookAuthors { get; set; }
    }
}
