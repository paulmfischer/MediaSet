using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MediaSet.Shared.ViewModels
{
    public class AddBookViewModel : MediaViewModel
    {
        [Required]
        [StringLength(255, ErrorMessage = "Title too long (255 character limit)")]
        public new string Title { get; set; }
        public string SubTitle { get; set; }
        public int? GenreId { get; set; }
        public int? NumberOfPages { get; set; }
        public int? PublisherId { get; set; }
        public PublisherViewModel Publisher { get; set; }
        public DateTime? PublicationDate { get; set; }
        public IList<AuthorViewModel> BookAuthors { get; set; } = new List<AuthorViewModel>();
        public IEnumerable<AuthorViewModel> Authors { get; set; } = new List<AuthorViewModel>();
        public IEnumerable<PublisherViewModel> Publishers { get; set; } = new List<PublisherViewModel>();
        public IEnumerable<FormatViewModel> Formats { get; set; } = new List<FormatViewModel>();
    }
}
