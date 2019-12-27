using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Authorizer.Decentralize
{
    public static class Extensions
    {
        public static SortedList<TKey, TValue> ToSortedList<TSource, TKey, TValue>
            (this IEnumerable<TSource> source,
             Func<TSource, TKey> keySelector,
             Func<TSource, TValue> valueSelector)
        {
            // TODO: Argument validation
            var ret = new SortedList<TKey, TValue>();
            foreach (var element in source)
            {
                ret.Add(keySelector(element), valueSelector(element));
            }
            return ret;
        }
    }
}
