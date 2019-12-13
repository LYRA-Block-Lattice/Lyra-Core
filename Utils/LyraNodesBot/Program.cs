using System;

namespace LyraNodesBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var monitor = new NodesMonitor();
            monitor.Main(args);
        }
    }
}
