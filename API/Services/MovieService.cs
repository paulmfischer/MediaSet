using Models;
using Repositories;
using Utilities.Extensions;

namespace Services;

public class MovieService : IMovieService
{
    IMovieRepository movieRepository;

    public MovieService(IMovieRepository movieRepository)
    {
        this.movieRepository = movieRepository;
    }
    
    public async Task<MovieModel> Create(CreateMovie data)
    {
        var movieEntity = await this.movieRepository.Create(data.MapToEntity());

        return movieEntity.MapToModel();
    }

    public Task Delete(int id)
    {
        return this.movieRepository.Delete(id);
    }

    public async Task<MovieModel?> Get(int id)
    {
        var movieEntity = await this.movieRepository.Get(id);
        return movieEntity?.MapToModel();
    }

    public IEnumerable<MovieModel> GetList()
    {
        var entities = this.movieRepository.GetList();
        var models = entities.Select(entity => entity.MapToModel());
        return models;
    }

    public async Task<MovieModel> Update(MovieModel data)
    {
        var entity = await this.movieRepository.Get(data.Id);
        if (entity == null) {
            throw new Exception("No entity exists");
        }

        var updatedEntity = await this.movieRepository.Update(data.MapToEntity());
        return updatedEntity.MapToModel();
    }
}