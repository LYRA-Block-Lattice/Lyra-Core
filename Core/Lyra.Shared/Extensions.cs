using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lyra.Shared
{
    public static class Extensions
    {
        public static Task AsTaskAsync(this WaitHandle handle)
        {
            return AsTaskAsync(handle, Timeout.InfiniteTimeSpan);
        }

        public static Task AsTaskAsync(this WaitHandle handle, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<object>();
            var registration = ThreadPool.RegisterWaitForSingleObject(handle, (state, timedOut) =>
            {
                var localTcs = (TaskCompletionSource<object>)state;
                if (timedOut)
                    localTcs.TrySetCanceled();
                else
                    localTcs.TrySetResult(null);
            }, tcs, timeout, executeOnlyOnce: true);
            _ = tcs.Task.ContinueWith((_, state) => ((RegisteredWaitHandle)state).Unregister(null), registration, TaskScheduler.Default);
            return tcs.Task;
        }

        public static string Json(this object o)
        {
            return JsonConvert.SerializeObject(o);
        }

        public static T UnJson<T>(this string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

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

            return $"{addr.Substring(0, 4)}...{addr.Substring(addr.Length - 6, 6)}";
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source, IEqualityComparer<T> comparer = null)
        {
            return new HashSet<T>(source, comparer);
        }
    }
}
