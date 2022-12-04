using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace API;

internal static class MovieApi
{
    public static RouteGroupBuilder MapMovie(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/movie");
        group.WithTags("Movie");

        group.MapGet("/", async (MediaSetContext context) =>
            await context.Movies
                .Include(movie => movie.Studio)
                .Include(movie => movie.Format)
                .Include(movie => movie.Genres)
                .AsNoTracking()
                .ToListAsync());

        group.MapGet("/{id}", async Task<Results<Ok<Movie>, NotFound>> (MediaSetContext context, int id) =>
            await context.Movies
                .Include(movie => movie.Studio)
                .Include(movie => movie.Format)
                .Include(movie => movie.Genres)
                .FirstOrDefaultAsync(movie => movie.Id == id) switch
            {
                Movie movie => TypedResults.Ok(movie),
                _ => TypedResults.NotFound()
            }
        );

        group.MapPost("/", async Task<Created<Movie>> (MediaSetContext context, Movie movie) =>
        {
            var associatedGenres = await context.Genres.Where(genre => movie.Genres.Select(g => g.Id).Contains(genre.Id)).ToListAsync();
            var newMovie = movie.AsNewMovie(associatedGenres);

            context.Movies.Add(newMovie);
            await context.SaveChangesAsync();

            var dbMovie = await context.Movies
                .Include(movie => movie.Studio)
                .Include(movie => movie.Format)
                .Include(movie => movie.Genres)
                .FirstOrDefaultAsync(movie => movie.Id == newMovie.Id);
            return TypedResults.Created($"/{newMovie.Id}", dbMovie);
        });

        group.MapPut("/{id}", async Task<Results<Ok<Movie>, NotFound, BadRequest>> (MediaSetContext context, int id, Movie movie) =>
        {
            if (id != movie.Id)
            {
                return TypedResults.BadRequest();
            }

            var dbMovie = await context.Movies
                .Include(movie => movie.Studio)
                .Include(movie => movie.Format)
                .Include(movie => movie.Genres)
                .FirstOrDefaultAsync(movie => movie.Id == id);

            if (dbMovie == null)
            {
                return TypedResults.NotFound();
            }
            var associatedGenres = await context.Genres.Where(genre => movie.Genres.Select(g => g.Id).Contains(genre.Id)).ToListAsync();

            dbMovie.Barcode = movie.Barcode;
            dbMovie.FormatId = movie.FormatId;
            dbMovie.Genres = associatedGenres;
            dbMovie.IsTvSeries = movie.IsTvSeries;
            dbMovie.Plot = movie.Plot;
            dbMovie.ReleaseDate = movie.ReleaseDate;
            dbMovie.Runtime = movie.Runtime;
            dbMovie.StudioId = movie.StudioId;
            dbMovie.Title = movie.Title;

            await context.SaveChangesAsync();

            return TypedResults.Ok(dbMovie);
        });

        return group;
    }
}