using System;

namespace MediaSet.Data.Models
{
    public class PersonalInformation
    {
        public int Id { get; set; }
        public int MediaId { get; set; }
        public Media Media { get; set; }
        public DateTime AddedDateTime { get; set; }
        public DateTime UpdatedDateTime { get; set; }
    }
}
