using Entities;
using Models;

namespace Utilities.Extensions;

public static class MovieMapper
{
    public static MovieModel MapToModel(this MovieEntity movieEntity)
    {
        return new MovieModel
        {
            Id = movieEntity.Id,
            ISBN = movieEntity.ISBN,
            IsTvSeries = movieEntity.IsTvSeries,
            Plot = movieEntity.Plot,
            ReleaseDate = movieEntity.ReleaseDate,
            Runtime = movieEntity.Runtime,
            SortTitle = movieEntity.SortTitle,
            Title = movieEntity.Title,
            UPC = movieEntity.UPC
        };
    }

    public static MovieEntity MapToEntity(this MovieModel movieModel)
    {
        return new MovieEntity
        {
            Id = movieModel.Id,
            ISBN = movieModel.ISBN,
            IsTvSeries = movieModel.IsTvSeries,
            Plot = movieModel.Plot,
            ReleaseDate = movieModel.ReleaseDate,
            Runtime = movieModel.Runtime,
            SortTitle = movieModel.SortTitle,
            Title = movieModel.Title,
            UPC = movieModel.UPC
        };
    }

    public static MovieEntity MapToEntity(this CreateMovie movieModel)
    {
        return new MovieEntity
        {
            ISBN = movieModel.ISBN,
            IsTvSeries = movieModel.IsTvSeries,
            Plot = movieModel.Plot,
            ReleaseDate = movieModel.ReleaseDate,
            Runtime = movieModel.Runtime,
            SortTitle = movieModel.SortTitle,
            Title = movieModel.Title,
            UPC = movieModel.UPC
        };
    }
}