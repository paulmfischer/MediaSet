using System;
using System.ComponentModel.DataAnnotations;

namespace MediaSet.Shared.ViewModels
{
    public class AddBookViewModel
    {
        [Required]
        [StringLength(255, ErrorMessage = "Title too long (255 character limit)")]
        public string Title { get; set; }
        public string ISBN { get; set; }
        public string SortTitle { get; set; }
        public string SubTitle { get; set; }
        public int? FormatId { get; set; }
        public int? GenreId { get; set; }
        public int? NumberOfPages { get; set; }
        public int? PublisherId { get; set; }
        public PublisherViewModel PublisherViewModel { get; set; }
        public DateTime? PublicationDate { get; set; }
    }
}
