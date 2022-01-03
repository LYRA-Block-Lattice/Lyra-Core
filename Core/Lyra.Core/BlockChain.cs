using Akka.Actor;
using Akka.Configuration;
using Akka.Streams.Util;
using Clifton.Blockchain;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Core.Decentralize;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using Neo;
using Neo.Cryptography.ECC;
using Neo.IO.Actors;
using Neo.Network.P2P.Payloads;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static Lyra.Core.Decentralize.ConsensusService;
using Settings = Neo.Settings;
using Lyra.Data.Utils;

namespace Lyra
{
    public class BlockChain : ReceiveActor
    {
        public class Startup { }
        public class PersistCompleted { }
        public class Import { }
        public class ImportCompleted { }
        public class BlockAdded
        {
            public Block NewBlock { get; set; }
        }

        public uint Height;
        public string NetworkID { get; private set; }

        private LyraConfig _nodeConfig;
        private readonly IAccountCollectionAsync _store;
        private DagSystem _sys;
        private ILogger _log;

        public BlockChain(DagSystem sys, IAccountCollectionAsync store)
        {
            _sys = sys;

            var nodeConfig = Neo.Settings.Default.LyraNode;
            _store = store; //new MongoAccountCollection();

            //_store = new LiteAccountCollection(Utilities.LyraDataDir);
            _log = new SimpleLogger("BlockChain").Logger;
            _nodeConfig = nodeConfig;
            NetworkID = nodeConfig.Lyra.NetworkId;

            Receive<Startup>(_ => { });
            Receive<Idle>(_ => { });
        }

        public static Props Props(DagSystem system, IAccountCollectionAsync store)
        {
            return Akka.Actor.Props.Create(() => new BlockChain(system, store)).WithMailbox("blockchain-mailbox");
        }

        #region storage api
        private async Task<bool> AddBlockImplAsync(Block block)
        {
            var result = await _store.AddBlockAsync(block);
            if (result)
            {
                _sys.Consensus.Tell(new BlockAdded { NewBlock = block });
            }
            return result;
        }

        #endregion
    }

    internal class BlockchainMailbox : PriorityMailbox
    {
        public BlockchainMailbox(Akka.Actor.Settings settings, Config config)
            : base(settings, config)
        {
        }

        internal protected override bool IsHighPriority(object message)
        {
            switch (message)
            {
                //case Header[] _:
                //case Block _:
                //case ConsensusPayload _:
                case Terminated _:
                    return true;
                default:
                    return false;
            }
        }
    }
}
