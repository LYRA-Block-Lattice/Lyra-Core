using Akka.Actor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Neo
{
    public class NeoSystem
    {
        public IActorRef Blockchain { get; }
        public IActorRef LocalNode { get; }
        internal IActorRef TaskManager { get; }
        public IActorRef Consensus { get; private set; }
    }


    public class Transaction
    {
        public List<string> Hash;
        public List<object> Witnesses;
    }

    public class Blockchain
    {
        public static Blockchain Singleton;
        public uint Height;
    }

    public class Snapshot
    {

    }

    public static class Extensions
    {
        unsafe internal static int ToInt32(this byte[] value, int startIndex)
        {
            fixed (byte* pbyte = &value[startIndex])
            {
                return *((int*)pbyte);
            }
        }
    }
}
