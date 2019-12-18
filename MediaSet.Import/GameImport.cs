using MediaSet.Data;
using MediaSet.Data.GameData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MediaSet.Import
{
    public static class GameImport
    {
        public static void Import(IList<string> dataRows)
        {
            var title = "Title";
            var platformTitle = "Platform";
            var genreTitle = "Genre";
            var releaseYear = "Release Year";
            var publisherTitle = "Publisher";
            var developerTitle = "Developer";
            var barcode = "Barcode";
            var subTitle = "Sub Title";
            var editionTitle = "Edition";
            var formatTitle = "Format";
            int GameMediaType = 3;

            var fields = dataRows[0].Split(";").Select((v, i) => new { Key = v, Value = i }).ToDictionary(o => o.Key, o => o.Value);

            IDictionary<string, Genre> genres = new Dictionary<string, Genre>();
            IDictionary<string, Platform> platforms = new Dictionary<string, Platform>();
            IDictionary<string, Publisher> publishers = new Dictionary<string, Publisher>();
            IDictionary<string, Developer> developers = new Dictionary<string, Developer>();
            IDictionary<string, Format> formats = new Dictionary<string, Format>();

            foreach (var gameData in dataRows.Skip(1))
            {
                var gameProperties = gameData.Split(";");

                var gameGenre = gameProperties[fields[genreTitle]].Trim();
                if (!string.IsNullOrWhiteSpace(gameGenre))
                {
                    foreach (var gen in gameGenre.Split(","))
                    {
                        var g = gen.Trim();
                        genres.AddIfDoesNotExist(g, () => new Genre { Name = g, MediaTypeId = GameMediaType });
                    }
                }

                var platformName = gameProperties[fields[platformTitle]].Trim();
                platforms.AddIfDoesNotExist(platformName);

                var developerName = gameProperties[fields[developerTitle]].Trim();
                developers.AddIfDoesNotExist(developerName);

                var publisher = gameProperties[fields[publisherTitle]].Trim();
                publishers.AddIfDoesNotExist(publisher, () => new Publisher { Name = publisher, MediaTypeId = GameMediaType });

                var formatName = gameProperties[fields[formatTitle]].Trim();
                formats.AddIfDoesNotExist(formatName, () => new Format { Name = formatName, MediaTypeId = GameMediaType });
            }

            using (var context = new MediaSetContext(attachLogging: true))
            {
                context.AddRange(platforms.Select(x => x.Value));
                context.AddRange(genres.Select(x => x.Value));
                context.AddRange(developers.Select(x => x.Value));
                context.AddRange(publishers.Select(x => x.Value));
                context.AddRange(formats.Select(x => x.Value));

                context.SaveChanges();
            }

            IList<Game> games = new List<Game>();

            foreach (var gameData in dataRows.Skip(1))
            {
                var gameProperties = gameData.Split(";");
                Platform platform;
                Developer developer;
                Publisher publisher;
                Format format;
                using (var context = new MediaSetContext())
                {
                    publisher = context.Publishers.FirstOrDefault(x => x.Name == gameProperties[fields[publisherTitle]].Trim());
                    platform = context.Platforms.FirstOrDefault(x => x.Name == gameProperties[fields[platformTitle]].Trim());
                    developer = context.Developers.FirstOrDefault(x => x.Name == gameProperties[fields[developerTitle]].Trim());
                    format = context.Formats.FirstOrDefault(x => x.Name == gameProperties[fields[formatTitle]].Trim());

                    var media = new Media
                    {
                        Title = gameProperties[fields[title]],
                        MediaTypeId = GameMediaType
                    };

                    if (format != null)
                    {
                        media.Format = format;
                        media.FormatId = format.Id;
                    }

                    var game = new Game
                    {
                        Media = media,
                        Barcode = gameProperties[fields[barcode]],
                        Platform = platform,
                        PlatformId = platform.Id,
                        ReleaseDate = gameProperties[fields[releaseYear]]
                    };

                    if (developer != null)
                    {
                        game.Developer = developer;
                        game.DeveloperId = developer.Id;
                    }

                    if (publisher != null)
                    {
                        game.Publisher = publisher;
                        game.PublisherId = publisher.Id;
                    }

                    media.Game = game;

                    var gameGenres = gameProperties[fields[genreTitle]].Trim();
                    if (!string.IsNullOrWhiteSpace(gameGenres))
                    {
                        media.MediaGenres = new List<MediaGenre>();
                        foreach (var gen in gameGenres.Split(","))
                        {
                            var genre = context.Genres.FirstOrDefault(x => x.Name == gen.Trim());
                            media.MediaGenres.Add(new MediaGenre { Genre = genre, GenreId = genre.Id, Media = game.Media, MediaId = game.Media.Id });
                        }
                    }

                    context.Games.Add(game);
                    context.SaveChanges();
                }
            }
        }
    }
}
