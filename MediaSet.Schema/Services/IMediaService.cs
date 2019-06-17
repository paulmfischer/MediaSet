using System;
using System.Collections.Generic;
using System.Text;

namespace MediaSet.Data.Services
{
    public interface IMediaService<TEntity>
    {
        IEnumerable<TEntity> GetAll();
        TEntity Get(int Id);
        TEntity Add(TEntity book);
        TEntity Update(TEntity book);
        void Delete(int Id);
    }
}
