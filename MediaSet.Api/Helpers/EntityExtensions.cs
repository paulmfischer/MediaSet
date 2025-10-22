using MediaSet.Api.Models;

namespace MediaSet.Api.Helpers;

internal static class EntityExtensions
{
    public static TEntity? SetType<TEntity>(this TEntity? entity) where TEntity : IEntity
    {
        if (entity == null)
            return default;

        var success = Enum.TryParse(typeof(MediaTypes), $"{entity.GetType().Name}s", true, out var parsedValue);
        if (success && parsedValue != null)
        {
            entity.Type = (MediaTypes)parsedValue;
        }
        return entity;
    }
}
