using MediaSet.Api.Helpers;
using MediaSet.Api.Models;
using MediaSet.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace MediaSet.Api.Books;

internal static class EntityApi
{
  public static RouteGroupBuilder MapEntity<TEntity>(this IEndpointRouteBuilder routes) where TEntity : IEntity
  {
    var entityName = $"{typeof(TEntity).Name}s";
    var group = routes.MapGroup($"/{entityName}");

    group.WithTags(entityName);

    group.MapGet("/", async (EntityService<TEntity> entityService) => await entityService.GetListAsync());
    group.MapGet("/search", async (EntityService<TEntity> entityService, string searchText, string orderBy) => await entityService.SearchAsync(searchText, orderBy));

    group.MapGet("/{id}", async Task<Results<Ok<TEntity>, NotFound>> (EntityService<TEntity> entityService, string id) =>
    {
      return await entityService.GetAsync(id) switch
      {
        TEntity entity => TypedResults.Ok(entity),
        _ => TypedResults.NotFound()
      };
    });

    group.MapPost("/", async Task<Results<Created<TEntity>, BadRequest>> (EntityService<TEntity> entityService, TEntity newEntity) =>
    {
      if (newEntity is null || newEntity.IsEmpty())
      {
        return TypedResults.BadRequest();
      }

      await entityService.CreateAsync(newEntity);

      return TypedResults.Created($"/{typeof(TEntity).Name}/{newEntity.Id}", newEntity);
    });

    group.MapPut("/{id}", async Task<Results<Ok, NotFound, BadRequest>> (EntityService<TEntity> entityService, string id, TEntity updatedEntity) =>
    {
      if (id != updatedEntity.Id)
      {
        return TypedResults.BadRequest();
      }

      var result = await entityService.UpdateAsync(id, updatedEntity);
      return result.ModifiedCount == 0 ? TypedResults.NotFound() : TypedResults.Ok();
    });

    group.MapDelete("/{id}", async Task<Results<NotFound, Ok>> (EntityService<TEntity> entityService, string id) =>
    {
      var result = await entityService.RemoveAsync(id);
      return result.DeletedCount == 0 ? TypedResults.NotFound() : TypedResults.Ok();
    });

    return group;
  }
}