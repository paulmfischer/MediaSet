using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Shared
{
    public class BookViewModel
    {
        public int Id { get; set; }
        public string ISBN { get; set; }
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string SubTitle { get; set; }
        //public Format Format { get; set; }
        public int FormatId { get; set; }
        //public Genre Genre { get; set; }
        public int GenreId { get; set; }
        public int NumberOfPages { get; set; }
        //public Publisher Publisher { get; set; }
        public int PublisherId { get; set; }
        public DateTime PublicationDate { get; set; }
        //public IList<Author> Authors { get; set; }
    }
}
