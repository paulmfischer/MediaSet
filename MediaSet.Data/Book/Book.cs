using System.Collections.Generic;

namespace MediaSet.Data.BookData
{
    public class Book : IEntity
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public Media Media { get; set; }
        public string ISBN { get; set; }
        public string SubTitle { get; set; }
        public int NumberOfPages { get; set; }
        public string PublicationDate { get; set; }
        public string Plot { get; set; }
        public string Dewey { get; set; }
        public ICollection<BookAuthor> BookAuthors { get; set; }
        public ICollection<BookPublisher> BookPublishers { get; set; }
    }
}
