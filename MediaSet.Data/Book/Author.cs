using System.Collections.Generic;

namespace MediaSet.Data.BookData
{
    public class Author : EntityAbstract
    {
        public ICollection<BookAuthor> BookAuthors { get; set; }
    }
}
