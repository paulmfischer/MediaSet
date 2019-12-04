using System;
using System.Collections.Generic;

namespace MediaSet.Data.BookData
{
    public class Book : IEntity
    {
        public int Id { get; set; }
        public int MediaTypeId { get; set;  }
        public Media Media { get; set; }
        public string SubTitle { get; set; }
        public string SortTitle { get; set; }
        public int NumberOfPages { get; set; }
        public DateTime PublicationDate { get; set; }
        public int PublisherId { get; set; }
        public Publisher Publisher { get; set; }
        public string Plot { get; set; }
        public float Dewey { get; set; }
        public ICollection<BookAuthor> BookAuthors { get; set; }
    }
}
