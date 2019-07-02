using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MediaSet.Data.Models
{
    public class Book : BaseMedia
    {
        public string SubTitle { get; set; }
        public int? NumberOfPages { get; set; }
        public int? PublisherId { get; set; }
        public Publisher Publisher { get; set; }
        [DataType(DataType.Date)]
        public DateTime? PublicationDate { get; set; }
        public ICollection<BookAuthor> BookAuthors { get; set; }
        //public virtual PersonalInformation PersonalInformation { get; set; }
    }
}
