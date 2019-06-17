using System.Collections.Generic;

namespace MediaSet.Data.Models
{
    public class Author
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string SortName { get; set; }
        public virtual ICollection<BookAuthor> BookAuthors { get; set; }
    }
}
