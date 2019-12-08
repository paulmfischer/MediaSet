using MediaSet.Data;
using MediaSet.Data.MovieData;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MediaSet.Import
{
    public static class StringExtensions
    {
        public static Movie FromCsv(this string movieRow)
        {
            string[] values = movieRow.Split(',');
            return values.FromArray();
        }

        public static Movie FromArray(this string[] movieRow)
        {
            var movie = new Movie
            {
                IMDBLink = movieRow[13],
                Plot = movieRow[12],
                ReleaseDate = movieRow[1],
                Runtime = movieRow[2]
            };

            movie.MovieStudios = new List<MovieStudio> { new MovieStudio { Studio = new Studio { Name = movieRow[14] } } };
            movie.Media = new Media
            {
                Barcode = movieRow[10],
                Title = movieRow[0],
                MediaTypeId = 2,
            };
            movie.Media.Format = new Format { Name = movieRow[8], MediaTypeId = 2 };
            var genres = movieRow[4];
            if (!string.IsNullOrWhiteSpace(genres))
            {
                var splitGenres = genres.Split(";");

                var mediaGenres = splitGenres.Select(x => new MediaGenre { Genre = new Genre { Name = x, MediaTypeId = 2 } }).ToList();
                movie.Media.MediaGenres = mediaGenres;
            }

            return movie;
        }
    }
}
