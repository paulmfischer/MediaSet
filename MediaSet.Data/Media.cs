using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data
{
    public class Media
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Barcode { get; set; }
        public string ISBN { get; set; }
        public int FormatId { get; set; }
        public int MediaTypeId { get; set; }
    }
}
