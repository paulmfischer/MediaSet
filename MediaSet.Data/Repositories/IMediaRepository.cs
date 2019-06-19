﻿using System.Collections.Generic;

namespace MediaSet.Data.Repositories
{
    public interface IMediaRepository<TEntity>
    {
        IEnumerable<TEntity> GetAll();
        TEntity Get(int Id);
        TEntity Add(TEntity book);
        TEntity Update(TEntity book);
        void Delete(int Id);
    }
}
