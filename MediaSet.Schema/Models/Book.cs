using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace MediaSet.Schema.Models
{
    public class Book : Media
    {
        public int NumberOfPages { get; set; }
        public Publisher Publisher { get; set; }
        public int PublisherId { get; set; }
        [DataType(DataType.Date)]
        public DateTime PublicationDate { get; set; }
        public virtual ICollection<BookAuthor> BookAuthors { get; set; }
    }
}
