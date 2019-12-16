using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data.BookData
{
    public class BookPublisher
    {
        public int BookId { get; set; }
        public Book Book { get; set; }
        public int PublisherId { get; set; }
        public Publisher Publisher { get; set; }
    }
}
