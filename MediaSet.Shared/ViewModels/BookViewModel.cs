using System;

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
        public PublisherViewModel PublisherViewModel { get; set; }
        public DateTime PublicationDate { get; set; }
    }
}
