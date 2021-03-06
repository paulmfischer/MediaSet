﻿using MediaSet.Data;
using MediaSet.Data.MovieData;
using System.Collections.Generic;
using System.Linq;

namespace MediaSet.Import
{
    public static class MovieImport
    {
        public static void Import(IList<string> dataRows)
        {
            var title = "Title";
            var imdb = "IMDb URL";
            var plot = "Plot";
            var releaseDate = "Released Year";
            var runtime = "Running Time";
            var studioTitle = "Studio";
            var barcode = "Barcode";
            var formatTitle = "Format";
            var genreTitle = "Genre";
            var producerTitle = "Producer";
            var directorTitle = "Director";
            var writerTitle = "Writer";
            int MovieMediaType = 2;
            var fields = dataRows[0].Split(",").Select((v, i) => new { Key = v, Value = i }).ToDictionary(o => o.Key, o => o.Value);

            IDictionary<string, Genre> genres = new Dictionary<string, Genre>();
            IDictionary<string, Studio> studios = new Dictionary<string, Studio>();
            IDictionary<string, Format> formats = new Dictionary<string, Format>();
            IDictionary<string, Producer> producers = new Dictionary<string, Producer>();
            IDictionary<string, Director> directors = new Dictionary<string, Director>();
            IDictionary<string, Writer> writers = new Dictionary<string, Writer>();

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

                var formatName = movieProperties[fields[formatTitle]].Trim();
                formats.AddIfDoesNotExist(formatName, () => new Format { Name = formatName, MediaTypeId = MovieMediaType });

                var studioName = movieProperties[fields[studioTitle]].Trim();
                if (!string.IsNullOrWhiteSpace(studioName))
                {
                    studios.AddIfDoesNotExist(studioName);
                }

                foreach (var prod in movieProperties[fields[producerTitle]].Split(";"))
                {
                    producers.AddIfDoesNotExist(prod.Trim());

                }
                foreach (var dir in movieProperties[fields[directorTitle]].Split(";"))
                {
                    directors.AddIfDoesNotExist(dir.Trim());

                }
                foreach (var writer in movieProperties[fields[writerTitle]].Split(";"))
                {
                    writers.AddIfDoesNotExist(writer.Trim());

                }
            }

            using (var context = new MediaSetContext())
            {
                context.AddRange(studios.Select(x => x.Value));
                context.AddRange(formats.Select(x => x.Value));
                context.AddRange(genres.Select(x => x.Value));
                context.AddRange(producers.Select(x => x.Value));
                context.AddRange(directors.Select(x => x.Value));
                context.AddRange(writers.Select(x => x.Value));

                context.SaveChanges();
            }

            IList<Movie> movies = new List<Movie>();

            foreach (var movieData in dataRows.Skip(1))
            {
                var movieProperties = movieData.Split(",");
                Format format;
                Studio studio;
                using (var context = new MediaSetContext())
                {
                    studio = context.Studios.FirstOrDefault(x => x.Name == movieProperties[fields[studioTitle]].Trim());
                    format = context.Formats.FirstOrDefault(x => x.Name == movieProperties[fields[formatTitle]].Trim());

                    var media = new Media
                    {
                        Title = movieProperties[fields[title]],
                        MediaTypeId = MovieMediaType,
                        Format = format,
                        FormatId = format.Id
                    };

                    var movie = new Movie
                    {
                        Barcode = movieProperties[fields[barcode]],
                        IMDBLink = movieProperties[fields[imdb]],
                        Plot = movieProperties[fields[plot]],
                        ReleaseDate = movieProperties[fields[releaseDate]],
                        Runtime = movieProperties[fields[runtime]],
                        Media = media
                    };
                    media.Movie = movie;

                    if (studio != null)
                    {
                        movie.Studio = studio;
                        movie.StudioId = studio.Id;
                    }

                    var movieGenre = movieProperties[fields[genreTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(movieGenre))
                    {
                        media.MediaGenres = new List<MediaGenre>();
                        foreach (var gen in movieGenre.Split(";"))
                        {
                            var genre = context.Genres.FirstOrDefault(x => x.Name == gen.Trim());
                            media.MediaGenres.Add(new MediaGenre { Genre = genre, GenreId = genre.Id, Media = movie.Media, MediaId = movie.Media.Id });
                        }
                    }

                    var movieDirectors = movieProperties[fields[directorTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(movieDirectors))
                    {
                        movie.MovieDirectors = new List<MovieDirector>();
                        foreach (var dir in movieDirectors.Split(";"))
                        {
                            var dbDirector = context.Directors.FirstOrDefault(x => x.Name == dir.Trim());
                            movie.MovieDirectors.Add(new MovieDirector { Director = dbDirector, DirectorId = dbDirector.Id, Movie = movie, MovieId = movie.Id });
                        }
                    }

                    var movieProducers = movieProperties[fields[producerTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(movieProducers))
                    {
                        movie.MovieProducers = new List<MovieProducer>();
                        foreach (var dir in movieProducers.Split(";"))
                        {
                            var dbProducer = context.Producers.FirstOrDefault(x => x.Name == dir.Trim());
                            movie.MovieProducers.Add(new MovieProducer { Producer = dbProducer, ProducerId = dbProducer.Id, Movie = movie, MovieId = movie.Id });
                        }
                    }

                    var movieWriters = movieProperties[fields[writerTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(movieWriters))
                    {
                        movie.MovieWriters = new List<MovieWriter>();
                        foreach (var dir in movieWriters.Split(";"))
                        {
                            var dbWriter = context.Writers.FirstOrDefault(x => x.Name == dir.Trim());
                            movie.MovieWriters.Add(new MovieWriter { Writer = dbWriter, WriterId = dbWriter.Id, Movie = movie, MovieId = movie.Id });
                        }
                    }

                    context.Movies.Add(movie);
                    context.SaveChanges();
                }
            }
        }
    }
}
