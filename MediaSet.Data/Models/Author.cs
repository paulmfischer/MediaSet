using System;
using System.Collections.Generic;
using System.Text;

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
