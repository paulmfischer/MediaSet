using MediaSet.Api.Filters;
using MediaSet.Data.Entities;
using MediaSet.Data.Repositories;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Api.Metadata;

internal static class MetadataApi
{
    public static RouteGroupBuilder MapEntity<TEntity>(this IEndpointRouteBuilder routes, string prefix) where TEntity : class, IMetadata
    {
        var group = routes.MapGroup(prefix);

        group.WithPrameterValidation(typeof(TEntity));

        group.MapGet("/", async (IMetadataRepository repo) => await repo.GetAll<TEntity>());

        group.MapGet("/{id}", async Task<Results<Ok<TEntity>, NotFound>> (IMetadataRepository repo, int id) =>
        {
            return await repo.GetById<TEntity>(id) switch 
            {
                TEntity entity => TypedResults.Ok(entity),
                _ => TypedResults.NotFound()
            };
        });

        group.MapPost("/", async Task<Created<TEntity>> (IMetadataRepository repo, TEntity entity) =>
        {
            await repo.Create(entity);

            return TypedResults.Created($"{prefix}/{entity.Id}", entity);
        });

        group.MapPut("/{id}", async Task<Results<Ok<TEntity>, NotFound, BadRequest<string>>> (IMetadataRepository repo, int id, TEntity entity) =>
        {
            if (id != entity.Id)
            {
                return TypedResults.BadRequest("Entity Id and route id do not match");
            }

            TEntity? updatedEntity = await repo.Update(entity);
            
            return updatedEntity is null ? TypedResults.NotFound() : TypedResults.Ok(entity);
        });

        group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (IMetadataRepository repo, int id) =>
        {
            var rowsAffected = await repo.DeleteById<TEntity>(id);
            
            return rowsAffected == 0 ? TypedResults.NotFound() : TypedResults.Ok();
        }); 

        return group;
    }
}