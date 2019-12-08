using MediaSet.Data;
using MediaSet.Data.MovieData;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MediaSet.Import
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var context = new MediaSetContext())
            {
                Console.WriteLine("Importing data for media type {0}", args[0]);
                var title = "Title";
                var imdb = "IMDb URL";
                var plot = "Plot";
                var releaseDate = "Released Year";
                var runtime = "Running Time";
                var studioTitle = "Studio";
                var barcode = "Barcode";
                var formatTitle = "Format";
                var genreTitle = "Genre";
                int MovieMediaType = 2;

                var file = @"C:\Users\pfischer\Documents\MovieExport2.txt";
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                IList<string> dataRows = File.ReadAllLines(file).ToList();
                var fields = dataRows[0].Split(",").Select((v, i) => new { Key = v, Value = i }).ToDictionary(o => o.Key, o => o.Value);

                IDictionary<string, Genre> genres = new Dictionary<string, Genre>();
                IDictionary<string, Studio> studios = new Dictionary<string, Studio>();
                IDictionary<string, Format> formats = new Dictionary<string, Format>();

                foreach (var movieData in dataRows.Skip(1))
                {
                    var movieProperties = movieData.Split(",");

                    var movieGenre = movieProperties[fields[genreTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(movieGenre))
                    {
                        foreach (var gen in movieGenre.Split(";"))
                        {
                            var g = gen.Trim();
                            if (!genres.ContainsKey(g))
                            {
                                genres.Add(g, new Genre { Name = g, MediaTypeId = MovieMediaType });
                            }
                        }
                    }

                    var studioName = movieProperties[fields[studioTitle]].Trim();
                    if (!studios.ContainsKey(studioName))
                    {
                        studios.Add(studioName, new Studio { Name = studioName });
                    }

                    var formatName = movieProperties[fields[formatTitle]].Trim();
                    if (!formats.ContainsKey(formatName))
                    {
                        formats.Add(formatName, new Format { Name = formatName, MediaTypeId = MovieMediaType });
                    }
                }

                context.AddRange(studios.Select(x => x.Value));
                context.AddRange(formats.Select(x => x.Value));
                context.AddRange(genres.Select(x => x.Value));
                context.SaveChanges();

                IList<Movie> movies = new List<Movie>();

                foreach (var movieData in dataRows.Skip(1))
                {
                    var movieProperties = movieData.Split(",");
                    var studio = context.Studios.FirstOrDefault(x => x.Name == movieProperties[fields[studioTitle]].Trim());
                    var format = context.Formats.FirstOrDefault(x => x.Name == movieProperties[fields[formatTitle]].Trim());

                    var movie = new Movie
                    {
                        IMDBLink = movieProperties[fields[imdb]],
                        Plot = movieProperties[fields[plot]],
                        ReleaseDate = movieProperties[fields[releaseDate]],
                        Runtime = movieProperties[fields[runtime]],
                        Media = new Media
                        {
                            Barcode = movieProperties[fields[barcode]],
                            Title = movieProperties[fields[title]],
                            MediaTypeId = MovieMediaType,
                            Format = format,
                            FormatId = format.Id
                        }
                    };
                    movie.MovieStudios = new List<MovieStudio> { new MovieStudio { Studio = studio, StudioId = studio.Id, Movie = movie } };

                    var movieGenre = movieProperties[fields[genreTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(movieGenre))
                    {
                        movie.Media.MediaGenres = new List<MediaGenre>();
                        foreach (var gen in movieGenre.Split(";"))
                        {
                            var genre = context.Genres.FirstOrDefault(x => x.Name == gen.Trim());
                            movie.Media.MediaGenres.Add(new MediaGenre { Genre = genre, GenreId = genre.Id, Media = movie.Media, MediaId = movie.Media.Id });
                        }
                    }

                    context.Movies.Add(movie);
                    context.SaveChanges();
                    //movies.Add(movie);
                }
                //context.AddRange(movies);
                //context.SaveChanges();

                stopwatch.Stop();
                Console.WriteLine("Elapsed time {0}", stopwatch.ElapsedMilliseconds);
                Console.ReadLine();
            }
        }
    }
}


//IList<Movie> movies = File.ReadAllLines(file)
//                               .Skip(1)
//                               .Select(v => v.FromCsv())
//                               .ToList();
//var studios = movies.SelectMany(x => x.MovieStudios.Select(y => y.Studio)).Distinct(new EntityComparer<Studio>()).ToList();
//context.Studios.AddRange(studios);
//context.SaveChanges();

//var genres = movies
//        .Where(x => x.Media.MediaGenres != null && x.Media.MediaGenres.Count > 0)
//        .SelectMany(x => x.Media?.MediaGenres?.Select(y => y.Genre)).Distinct(new EntityComparer<Genre>()).ToList();
//context.Genres.AddRange(genres);
//context.SaveChanges();

//var formats = movies.Select(x => x.Media.Format).Distinct(new EntityComparer<Format>()).ToList();
//context.Formats.AddRange(formats);
//context.SaveChanges();

////var media = movies.Select(x => x.Media);
////context.Media.AddRange(media);
////context.SaveChanges();

//foreach (var movie in movies)
//{
//    Console.WriteLine("Movie Name: {0}", movie.Media.Title);
//    movie.Media.Format = formats.FirstOrDefault(x => x.Name.Equals(movie.Media.Format.Name));
//    context.Media.Add(movie.Media);
//    context.SaveChanges();

//    movie.MediaId = movie.Media.Id;
//    foreach (var movieStudio in movie.MovieStudios)
//    {
//        movieStudio.Studio = studios.FirstOrDefault(x => x.Name.Equals(movieStudio.Studio.Name));
//        movieStudio.StudioId = movieStudio.Studio.Id;
//        movieStudio.Movie = movie;
//        movieStudio.MovieId = movie.Id;
//    }

//    foreach (var movieGenre in movie.Media.MediaGenres)
//    {
//        movieGenre.Genre = genres.FirstOrDefault(x => x.Name.Equals(movieGenre.Genre.Name));
//        movieGenre.GenreId = movieGenre.Genre.Id;
//        movieGenre.Media = movie.Media;
//        movieGenre.MediaId = movie.MediaId;
//    }
//    context.Movies.Add(movie);
//    context.SaveChanges();
//}
//context.AddRange(movies);
//context.SaveChanges();