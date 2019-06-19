using MediaSet.Data.Models;
using System.Collections.Generic;

namespace MediaSet.Data.Repositories
{
    public interface IMetadataRepository
    {
        IEnumerable<TEntity> GetAll<TEntity>() where TEntity : class, IEntity;
    }
}
