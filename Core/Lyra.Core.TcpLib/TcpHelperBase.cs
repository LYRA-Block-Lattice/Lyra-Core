using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace TcpHelperLib
{
    public class TcpHelperBase
    {
        #region Vars

        private long timeOfLastInteraction = DateTime.MinValue.Ticks;
        protected static int logLevel;

        #endregion // Vars
       
        #region Time Related

        public TimeSpan TimeSpanSinceLastInteraction
        {
            get { return DateTime.Now - new DateTime(Interlocked.Read(ref timeOfLastInteraction)); }
        }

        protected void SetLastInteractionTime()
        {
            Interlocked.Exchange(ref timeOfLastInteraction, DateTime.Now.Ticks);
        }

        #endregion // Time Related

        #region Log

        protected virtual void Log(string s, int level = 0)
        {
            if (logLevel >= level)
                Console.WriteLine($"        TcpHelperLib: {s}");
        }

        protected virtual void LogError(string s, Exception e = null)
        {
            Console.WriteLine($"        TcpHelperLib: ***** ERROR: {s} {e}");
        }

        #endregion // Log
    }
}
