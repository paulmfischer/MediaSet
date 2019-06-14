using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Schema.Models
{
    public class Format
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public MediaType MediaType { get; set; }
    }
}
