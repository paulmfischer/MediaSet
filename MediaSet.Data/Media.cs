﻿using MediaSet.Data.BookData;
using MediaSet.Data.MovieData;
using System.Collections.Generic;

namespace MediaSet.Data
{
    public class Media
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Barcode { get; set; }
        public string ISBN { get; set; }
        public int? FormatId { get; set; }
        public Format Format { get; set; }
        public int MediaTypeId { get; set; }
        public ICollection<MediaGenre> MediaGenres { get; set; }
        public Movie Movie { get; set; }
        public Book Book { get; set; }
    }
}