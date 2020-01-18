using System.Collections.Generic;

namespace MediaSet.Data
{
    public class PagedResult<T>
    {
        public IList<T> Items { get; set; }
        public int Total { get; set; }
    }
}
