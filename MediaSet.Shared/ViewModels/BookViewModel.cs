using System;
using System.Collections.Generic;

namespace MediaSet.Shared.ViewModels
{
    public class BookViewModel : MediaViewModel
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public string SubTitle { get; set; }
        public int? GenreId { get; set; }
        public int? NumberOfPages { get; set; }
        public int? PublisherId { get; set; }
        public PublisherViewModel Publisher { get; set; }
        public DateTime PublicationDate { get; set; }
        public IEnumerable<AuthorViewModel> Authors { get; set; } = new List<AuthorViewModel>();
    }
}
