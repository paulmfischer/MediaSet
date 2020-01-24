using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MediaSet.Data.Repositories
{
    public abstract class BaseRepository<T> where T : IMedia
    {
        public abstract IQueryable<T> GetBaseQuery();
        public abstract Task<int> GetTotalEntities();
        public abstract IQueryable<T> SearchEntityQuery(string filterValue);

        public async Task<IList<T>> GetAll()
        {
            return await this.GetBaseQuery().ToListAsync();
        }

        public async Task<T> GetById(int id)
        {
            return await this.GetBaseQuery().FirstOrDefaultAsync(entity => entity.Id == id);
        }

        public async Task<PagedResult<T>> Paged(int skip, int take, string filterValue)
        {
            var query = this.SearchEntityQuery(filterValue);

            return new PagedResult<T>
            {
                Items = await query.Skip(skip).Take(take).ToListAsync(),
                Total = await query.CountAsync()
            };
        }
    }
}
