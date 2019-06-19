using MediaSet.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace MediaSet.Data.Repositories
{
    public class MetadataRepository : IMetadataRepository
    {
        private readonly IMediaSetDbContext dbContext;

        public MetadataRepository(IMediaSetDbContext context)
        {
            dbContext = context;
        }

        public IEnumerable<TEntity> GetAll<TEntity>() where TEntity : class, IEntity
        {
            return dbContext.Set<TEntity>().AsNoTracking();
        }
    }
}
