using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MediaSet.Data.Repositories
{
    public class BaseRepository
    {
        private readonly MediaSetContext context;

        public BaseRepository(MediaSetContext context)
        {
            this.context = context;
        }

        //public IEnumerable<T> GetAll<T>()
        //{
        //    return this.context.
        //}
    }
}
