using MediaSet.Api.Filters;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Api.Metadata;

internal static class MetadataApi
{
    public static RouteGroupBuilder MapEntity<TEntity>(this IEndpointRouteBuilder routes, string prefix) where TEntity : class, IMetadata
    {
        var group = routes.MapGroup(prefix);

        group.WithPrameterValidation(typeof(TEntity));

        group.MapGet("/", async (MediaSetDbContext db) => await db.GetDbSet<TEntity>().AsNoTracking().ToListAsync());

        group.MapGet("/{id}", async Task<Results<Ok<TEntity>, NotFound>> (MediaSetDbContext db, int id) =>
        {
            return await db.GetDbSet<TEntity>().FirstOrDefaultAsync(entity => entity.Id == id) switch 
            {
                TEntity entity => TypedResults.Ok(entity),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/", async Task<Created<TEntity>> (MediaSetDbContext db, TEntity entity) =>
        {
            db.GetDbSet<TEntity>().Add(entity);
            await db.SaveChangesAsync();

            return TypedResults.Created($"{prefix}/{entity.Id}", entity);
        });

        group.MapPut("/{id}", async Task<Results<Ok<TEntity>, NotFound, BadRequest<string>>> (MediaSetDbContext db, int id, TEntity entity) =>
        {
            if (id != entity.Id)
            {
                return TypedResults.BadRequest("Entity Id and route id do not match");
            }

            var rowsAffected = await db.GetDbSet<TEntity>().Where(f => f.Id == id)
                .ExecuteUpdateAsync(updates =>
                    updates.SetProperty(f => f.Name, entity.Name)
                );
            
            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok(entity);
        });

        group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (MediaSetDbContext db, int id) =>
        {
            var rowsAffected = await db.GetDbSet<TEntity>().Where(f => f.Id == id)
                                    .ExecuteDeleteAsync();
            
            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok();
        }); 

        return group;
    }
}

internal static class MediaSetDbContextExtensions
{
    public static DbSet<TEntity> GetDbSet<TEntity>(this MediaSetDbContext db) where TEntity : class
    {
        Type entityType = typeof(TEntity);

        if (entityType == typeof(Format))
        {
            return db.Formats as DbSet<TEntity>;
        }
        else if (entityType == typeof(Genre))
        {
            return db.Genres as DbSet<TEntity>;
        }
        else //if (entityType == typeof(Publisher))
        {
            return db.Publishers as DbSet<TEntity>;
        }
    }
}