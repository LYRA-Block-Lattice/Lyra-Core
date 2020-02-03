using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Shared
{
    public class StopWatcher
    {
        private static ConcurrentDictionary<string, List<StopwatcherData>> _data = new ConcurrentDictionary<string, List<StopwatcherData>>();

        public static ConcurrentDictionary<string, List<StopwatcherData>> Data => _data;

        public static void Reset()
        {
            _data = new ConcurrentDictionary<string, List<StopwatcherData>>();
        }

        public static void Track(Action action, string message)
        {
            var w = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                w.Stop();
                List<StopwatcherData> list;
                if (_data.ContainsKey(message))
                    list = _data[message];
                else
                {
                    list = new List<StopwatcherData>();
                    _data.TryAdd(message, list);
                }
                list.Add(new StopwatcherData(w.ElapsedMilliseconds, message));
            }
        }

        //public static T Track<T>(Func<T> func, string message)
        //{
        //    var w = Stopwatch.StartNew();
        //    try
        //    {
        //        return func();
        //    }
        //    finally
        //    {
        //        w.Stop();
        //        _data.Add(new StopwatcherData(w.ElapsedMilliseconds, message));
        //    }
        //}

        public async static Task<T> Track<T>(Task<T> func, string message)
        {
            var w = Stopwatch.StartNew();
            try
            {
                return await func;
            }
            finally
            {
                w.Stop();
                List<StopwatcherData> list;
                if (_data.ContainsKey(message))
                    list = _data[message];
                else
                {
                    list = new List<StopwatcherData>();
                    _data.TryAdd(message, list);
                }
                list.Add(new StopwatcherData(w.ElapsedMilliseconds, message));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetCurrentMethod()
        {
            var st = new StackTrace();

            for(int i = 0; i < 10; i++)
            {
                var sf = st.GetFrame(i);
                var name = sf.GetMethod().Name;
                if (name.EndsWith("Async"))
                    return name;
            }

            return "unknown";
        }
    }

    public class StopwatcherData
    {
        public long MS { get; set; }
        public string Message { get; set; }
        public StopwatcherData(long ms, string msg)
        {
            MS = ms;
            Message = msg;
        }
    }
}
