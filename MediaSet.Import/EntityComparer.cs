using MediaSet.Data;
using MediaSet.Data.MovieData;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace MediaSet.Import
{
    public class EntityComparer<T> : IEqualityComparer<T> where T : EntityAbstract
    {
        public bool Equals([AllowNull] T x, [AllowNull] T y)
        {
            return x.Name.Equals(y.Name);
        }

        public int GetHashCode([DisallowNull] T obj)
        {
            return obj.Id;
        }
    }
}
