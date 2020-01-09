using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Lyra.Core.Utils;
using Akka.Actor;

namespace Lyra.Core.Accounts
{
    public class ServiceAccount : UntypedActor
    {
        public const string SERVICE_ACCOUNT_NAME = "service_account";
        public string DatabasePath { get; set; }

        Timer timer = null;

        private readonly IActorRef _blockChain;
        private readonly Wallet _svcWallet;
        private LyraConfig _config;

        public bool IsNodeFullySynced { get; set; }

        //public Dictionary<string, string> TokenGenesisBlocks { get; set; }

        public ServiceAccount(IActorRef blockChain, Wallet svcWallet)
        {
            _blockChain = blockChain;
            _svcWallet = svcWallet;
            _config = Neo.Settings.Default.LyraNode;
        }

        public static Props Props(IActorRef blockChain, Wallet svcWallet)
        {
            return Akka.Actor.Props.Create(() => new ServiceAccount(blockChain, svcWallet));
        }

        public ServiceBlock GetLastServiceBlock()
        {
            return BlockChain.Singleton.GetLastServiceBlock();
            ////var lstServiceBlock = base._storage. _blocks.FindOne(Query.And(Query.EQ("AccountID", AccountId), Query.EQ("SourceHash", sendBlock.Hash)));
            //Block lastBlock = GetLatestBlock();
            //if (lastBlock.BlockType == BlockTypes.Service)
            //    return lastBlock as ServiceBlock;
            //if (lastBlock == null)
            //    return null;
            //string hash = (lastBlock as SyncBlock).LastServiceBlockHash;
            //ServiceBlock lastServiceBlock = FindBlockByHash(hash) as ServiceBlock;
            //return lastServiceBlock;
        }

        //public void InitializeServiceAccountAsync(string Path)
        //{
        //    //CreateAccountAsync(Path, SERVICE_ACCOUNT_NAME, AccountTypes.Service);
        //    //_blocks.EnsureIndex(x => x.AccountID);
        //    //_blocks.EnsureIndex(x => x.Index);

        //    ServiceBlock firstServiceBlock = new ServiceBlock()
        //    {
        //        Authorizers = new List<NodeInfo>(),                //new Dictionary<short, NodeInfo>(),
        //        TransferFee = 1,  // 1 LYR
        //        TokenGenerationFee = 100, // 100 LYR
        //        TradeFee = 0.1m, // 0.1 LYR
        //        IsPrimaryShard = true,
        //        AcceptedShards = new List<string> { "Primary" },
        //    };

        //    firstServiceBlock.Authorizers.Add(new NodeInfo() { PublicKey = _svcWallet.AccountId, IPAddress = "127.0.0.1" });
        //    firstServiceBlock.InitializeBlock(null, _svcWallet.PrivateKey, _config.Lyra.NetworkId, AccountId: _svcWallet.AccountId);

        //    //firstServiceBlock.Signature = Signatures.GetSignature(PrivateKey, firstServiceBlock.Hash);
        //    BlockChain.Singleton.AddBlock(firstServiceBlock);
        //}

        public void Start(bool ModeConsensus, string Path)
        {
        //    IsNodeFullySynced = true;

        //    if (!AccountExistsLocally(Path, SERVICE_ACCOUNT_NAME))
        //    {
        //        InitializeServiceAccountAsync(Path);
        //    }
        //    else
        //        OpenAccount(Path, SERVICE_ACCOUNT_NAME);
        //    DatabasePath = Path;

        //    // begin sync node

        //    if (!ModeConsensus)
        //    {
        //        timer = new Timer(_ =>
        //        {
        //            TimingSync();
        //        },
        //        null, 10 * 1000, 10 * 60 * 1000);
        //    }
        }

        public void TimingSync()
        {
            //try
            //{
            //    Block latestBlock = GetLatestBlock();
            //    if (latestBlock == null)
            //        throw new Exception("Last service chain block not found!");

            //    ServiceBlock latestServiceBlock;
            //    if (latestBlock.BlockType != BlockTypes.Service)
            //    {
            //        latestServiceBlock = GetLastServiceBlock();
            //        if (latestServiceBlock == null)
            //            throw new Exception("Latest service block not found!");
            //    }
            //    else
            //        latestServiceBlock = latestBlock as ServiceBlock;

            //    SyncBlock sync = new SyncBlock();
            //    sync.LastServiceBlockHash = latestServiceBlock.Hash;
            //    sync.InitializeBlock(latestBlock, PrivateKey, NetworkId, AccountId: AccountId);

            //    //sync.Signature = Signatures.GetSignature(PrivateKey, sync.Hash);
            //    BlockChain.Singleton.AddBlock(sync);
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("Exception in StartSingleNodeTestnet timer procedure: " + e.Message);
            //}
        }

        protected override void OnReceive(object message)
        {
            throw new NotImplementedException();
        }
    }

}

