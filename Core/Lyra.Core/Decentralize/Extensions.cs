using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Decentralize
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

        public static string Shorten(this string addr)
        {
            if (string.IsNullOrWhiteSpace(addr) || addr.Length < 10)
                return addr;

            return $"{addr.Substring(0, 3)}...{addr.Substring(addr.Length - 6, 6)}";
        }
    }
}
