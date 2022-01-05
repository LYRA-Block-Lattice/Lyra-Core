using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.Decentralize.WorkFlow;
using Lyra.Data.API;
using Lyra.Data.Shared;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class BrokerWorkFlow
    {
        //public APIResultCodes EntryAuth(DagSystem sys, SendTransferBlock send, TransactionBlock lastBlock)
        //{
            
        //}
    }

    [BsonIgnoreExtraElements]
    public class BrokerBlueprint
    {
        // properties
        public long view { get; set; }
        public DateTime start { get; set; }
        public string initiatorAccount { get; set; }
        public string brokerAccount { get; set; }
        //public bool exclusive { get; set; }

        // work flow
        public string action { get; set; }
        public string svcReqHash { get; set; }   // this is the key

        // pre step, pf recv
        public bool preDone { get; set; }

        // main step, broker operation, can be null
        public bool mainDone { get; set; }

        // extra step
        public bool extraDone { get; set; }

        public bool FullDone => preDone && mainDone && extraDone;

        public DateTime LastBlockTime { get; private set; }

        public BrokerBlueprint()
        {

        }

        public async Task<bool> ExecuteAsync(DagSystem sys, Func<TransactionBlock, Task> submit, string caller)
        {
            Console.WriteLine($"execute bp by {caller}: {svcReqHash} for {action}");
            // execute work flow
            var wf = BrokerFactory.DynWorkFlows[action];
            var send = await sys.Storage.FindBlockByHashAsync(svcReqHash) as SendTransferBlock;
            if (send == null)
                return false;

            if (!preDone)
            {
                if(wf.GetDescription().RecvVia == BrokerRecvType.PFRecv)
                {
                    var preBlock = await BrokerOperations.ReceivePoolFactoryFeeAsync(sys, send);
                    if (preBlock == null)
                        preDone = true;
                    else
                    {
                        LastBlockTime = DateTime.UtcNow;
                        await submit(preBlock);
                        preDone = false;
                        Console.WriteLine($"WF: {send.Hash.Shorten()} preDone: {preDone}");
                    }
                }
                else if(wf.GetDescription().RecvVia == BrokerRecvType.DaoRecv)
                {
                    var preBlock = await BrokerOperations.ReceiveDaoFeeAsync(sys, send);
                    if (preBlock == null)
                        preDone = true;
                    else
                    {
                        LastBlockTime = DateTime.UtcNow;
                        await submit(preBlock);
                        preDone = false;
                        Console.WriteLine($"WF: {send.Hash.Shorten()} preDone: {preDone}");
                    }
                }
                else
                {
                    preDone = true;
                }
            }

            if(preDone && !mainDone)
            {
                var mainBlock = await wf.BrokerOpsAsync(sys, send);
                if (mainBlock == null)
                    mainDone = true;
                else
                {
                    // send it
                    LastBlockTime = DateTime.UtcNow;
                    await submit(mainBlock);
                    mainDone = false;
                    Console.WriteLine($"WF: {send.Hash.Shorten()} {mainBlock.BlockType} mainDone: {mainDone}");
                }
            }

            if (preDone && mainDone && !extraDone)
            {
                var otherBlocks = await wf.ExtraOpsAsync(sys, svcReqHash);
                if (otherBlocks == null)
                {
                    extraDone = true;
                }
                else
                {
                    // foreach block send it
                    LastBlockTime = DateTime.UtcNow;
                    await submit(otherBlocks);
                    Console.WriteLine($"WF: {send.Hash.Shorten()} {otherBlocks.BlockType}: extraDone: {false}");
                    extraDone = false;
                }
            }
            return FullDone;
        }
    }

    public class LyraWorkFlowAttribute : Attribute
    {

    }
    public enum BrokerRecvType { None, PFRecv, DaoRecv }
    public class BrokerFactory
    {
        public static Dictionary<string, WorkFlowBase> DynWorkFlows;
        
        public static ConcurrentDictionary<string, BrokerBlueprint> Bps { get; set; }

        public static event Action<BrokerBlueprint> OnFinished;

        private void AddWorkFlow(AuthorizersFactory af, IAccountCollectionAsync store, WorkFlowBase workflow)
        {
            var desc = workflow.GetDescription();
            DynWorkFlows.Add(desc.Action, workflow);

            foreach(var bd in desc.Blocks)
            {
                if (bd.AuthorizerName != null)
                    af.Register(bd.BlockType, bd.AuthorizerName);

                if (bd.TheBlock != null)
                {
                    store?.Register(bd.TheBlock);
                    BlockAPIResult.Register(bd.BlockType, bd.TheBlock);
                }                    
            }
        }

        private IEnumerable<Type> GetTypesWithMyAttribute(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                if (Attribute.IsDefined(type, typeof(LyraWorkFlowAttribute)))
                    yield return type;
            }
        }

        public void Init(AuthorizersFactory af, IAccountCollectionAsync store)
        {
            if (DynWorkFlows != null)
                return;
                //throw new InvalidOperationException("Already initialized.");

            DynWorkFlows = new Dictionary<string, WorkFlowBase>();
            Bps = new ConcurrentDictionary<string, BrokerBlueprint>();

            foreach(var type in GetTypesWithMyAttribute(Assembly.GetExecutingAssembly()))
            {
                var lyrawf = (WorkFlowBase)Activator.CreateInstance(type);
                AddWorkFlow(af, store, lyrawf);
            }
        }

        public static void CreateBlueprint(BrokerBlueprint blueprint)
        {
            Console.WriteLine($"create bp: {blueprint.svcReqHash}");
            Bps.TryAdd(blueprint.svcReqHash, blueprint);
        }
        public static BrokerBlueprint GetBlueprint(string relatedTx)
        {
            if (Bps.ContainsKey(relatedTx))
                return Bps[relatedTx];
            else
                return null;
        }
        public static void RemoveBlueprint(string hash)
        {
            if (Bps.ContainsKey(hash))
            {
                BrokerBlueprint bp;
                Console.WriteLine($"remove bp: {hash}");
                bool rmv = Bps.TryRemove(hash, out bp);
                if (!rmv)
                    Console.WriteLine("Bps.TryRemove error!");

                if(OnFinished != null && bp != null)
                    OnFinished(bp);
            }                
        }
        public static long UpdateBlueprint(BrokerBlueprint bp)
        {
            if (Bps.ContainsKey(bp.svcReqHash))
            {
                Bps[bp.svcReqHash] = bp;
                return 1;
            }
            else
            {
                return 0;
            }
        }

        public static List<BrokerBlueprint> GetAllBlueprints()
        {
            return Bps.Values.ToList();
        }

        public static void Load(IAccountCollectionAsync stor)
        {
            var bps = stor.GetAllBlueprints();
            foreach (var bp in bps)
            {
                if (!Bps.Keys.Any(a => a == bp.svcReqHash))
                    Bps.TryAdd(bp.svcReqHash, bp);
            }
        }

        public static void Persist(IAccountCollectionAsync stor)
        {
            // save to database
            var storeBps = stor.GetAllBlueprints();
            foreach (var bp in storeBps)
            {
                if(!Bps.Keys.Any(a => a == bp.svcReqHash))
                    stor.RemoveBlueprint(bp.svcReqHash);
            }

            storeBps = stor.GetAllBlueprints();

            foreach (var n in Bps)
            {
                if (storeBps.Any(a => a.svcReqHash == n.Key))
                    stor.UpdateBlueprint(n.Value);
                else
                    stor.CreateBlueprint(n.Value);
            }    
        }

        public static string GetBrokerAccountID(SendTransferBlock send)
        {
            string action = null;
            if (send.Tags != null && send.Tags.ContainsKey(Block.REQSERVICETAG))
                action = send.Tags[Block.REQSERVICETAG];

            string brkaccount;
            switch (action)
            {
                case BrokerActions.BRK_PFT_GETPFT:
                    brkaccount = send.Tags["pftid"];
                    break;
                case BrokerActions.BRK_POOL_ADDLQ:
                case BrokerActions.BRK_POOL_SWAP:
                case BrokerActions.BRK_POOL_RMLQ:
                    brkaccount = send.Tags["poolid"];
                    break;
                case BrokerActions.BRK_STK_ADDSTK:
                    brkaccount = send.DestinationAccountId;
                    break;
                case BrokerActions.BRK_STK_UNSTK:
                    brkaccount = send.Tags["stkid"];
                    break;
                default:
                    brkaccount = null;
                    break;
            };
            return brkaccount;
        }
    }
}
