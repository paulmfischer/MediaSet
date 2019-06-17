using System;

namespace MediaSet.Shared.ViewModels
{
    public class BookViewModel
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public string ISBN { get; set; }
        public string Title { get; set; }
        public string SortTitle { get; set; }
        public string SubTitle { get; set; }
        public int? FormatId { get; set; }
        public int? GenreId { get; set; }
        public int? NumberOfPages { get; set; }
        public int? PublisherId { get; set; }
        public DateTime PublicationDate { get; set; }
    }
}
