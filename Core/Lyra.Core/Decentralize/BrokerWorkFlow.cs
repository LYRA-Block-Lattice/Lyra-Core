using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Shared;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class BrokerWorkFlow
    {
        //public Func<DagSystem, SendTransferBlock, Task<ReceiveTransferBlock>> pfOps { get; set; }
        //public Func<DagSystem, SendTransferBlock, Task<TransactionBlock>> brokerOps { get; set; }
        //public Func<DagSystem, TransactionBlock?, Task<List<TransactionBlock>>> extraOps { get; set; }


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
        public bool prePending { get; set; }
        public string preHash { get; set; }

        // main step, broker operation, can be null
        public bool mainDone { get; set; }
        public Dictionary<string, string> mainPendings { get; set; }      // has 

        // extra step
        public bool extraDone { get; set; }
        public Dictionary<string, string> extraPendings { get; set; }

        public bool FullDone => preDone && mainDone && extraDone;

        public BrokerBlueprint()
        {
            // key => hash. check the hash to make sure block exists.
            mainPendings = new Dictionary<string, string>();
            extraPendings = new Dictionary<string, string>();
        }

        public void Reset()
        {
            prePending = false;
            mainPendings.Clear();
            extraPendings.Clear();
        }

        public async Task<bool> ExecuteAsync(DagSystem sys, bool IsLeader, Func<TransactionBlock, Task> submit)
        {
            if(!IsLeader)
            {
                Reset();
            }
            // execute work flow
            var wf = BrokerFactory.WorkFlows[action];
            var send = await sys.Storage.FindBlockByHashAsync(svcReqHash) as SendTransferBlock;
            if (!preDone)
            {
                if(wf.pfrecv)
                {
                    // block pending consensus
                    if (prePending && await sys.Storage.FindBlockByHashAsync(preHash) == null)
                        return false;

                    var preBlock = await BrokerOperations.ReceivePoolFactoryFeeAsync(sys, this, send);
                    if (preBlock == null)
                        preDone = true;
                    else
                    {
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
                if(wf.brokerOps != null)
                {
                    // check pending blocks
                    foreach(var kvp in mainPendings)
                    {
                        var blk = await sys.Storage.FindBlockByHashAsync(kvp.Value);
                        if (blk == null)
                            return false;
                    }

                    var mainBlock = await wf.brokerOps(sys, this, send);
                    if (mainBlock == null)
                        mainDone = true;
                    else
                    {
                        // send it
                        await submit(mainBlock);
                        mainDone = false;
                        Console.WriteLine($"WF: {send.Hash.Shorten()} {mainBlock.BlockType} mainDone: {mainDone}");
                    }
                }
                else
                {
                    mainDone = true;
                }
            }

            if (preDone && mainDone && !extraDone)
            {
                if(wf.extraOps != null)
                {
                    // check pending blocks
                    foreach (var kvp in extraPendings)
                    {
                        var blk = await sys.Storage.FindBlockByHashAsync(kvp.Value);
                        if (blk == null)
                            return false;
                    }

                    var otherBlocks = await wf.extraOps(sys, this, svcReqHash);
                    if (otherBlocks == null)
                    {
                        extraDone = true;
                    }
                    else
                    {
                        // foreach block send it
                        await submit(otherBlocks);
                        Console.WriteLine($"WF: {send.Hash.Shorten()} {otherBlocks.BlockType}: extraDone: {false}");
                        extraDone = false;
                    }
                }
                else
                {
                    extraDone = true;
                }
            }
            return FullDone;
        }
    }

    public class BrokerFactory
    {
        public static Dictionary<string, (bool pfrecv, Func<DagSystem, BrokerBlueprint, SendTransferBlock, Task<TransactionBlock>> brokerOps, Func<DagSystem, BrokerBlueprint, string, Task<TransactionBlock>> extraOps)> WorkFlows { get; set; }

        public static ConcurrentDictionary<string, BrokerBlueprint> Bps { get; set; }
        public void Init()
        {
            if (WorkFlows != null)
                throw new InvalidOperationException("Already initialized.");

            WorkFlows = new Dictionary<string, (bool pfrecv, Func<DagSystem, BrokerBlueprint, SendTransferBlock, Task<TransactionBlock>> brokerOps, Func<DagSystem, BrokerBlueprint, string, Task<TransactionBlock>> extraOps)>();
            Bps = new ConcurrentDictionary<string, BrokerBlueprint>();

            // liquidate pool
            WorkFlows.Add(BrokerActions.BRK_POOL_CRPL, (true, BrokerOperations.CNOCreateLiquidatePoolAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_ADDLQ, (false, BrokerOperations.AddPoolLiquidateAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_RMLQ, (true, BrokerOperations.SendWithdrawFundsAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_SWAP, (false, BrokerOperations.ReceivePoolSwapInAsync, BrokerOperations.SendPoolSwapOutTokenAsync));

            // profiting
            WorkFlows.Add(BrokerActions.BRK_PFT_CRPFT, (true, BrokerOperations.CNOCreateProfitingAccountAsync, null));
            //WorkFlows.Add(BrokerActions.BRK_PFT_FEEPFT, (true, BrokerOperations.SyncNodeFeesAsync, null));
            WorkFlows.Add(BrokerActions.BRK_PFT_GETPFT, (true, BrokerOperations.CNOReceiveAllProfitAsync, BrokerOperations.CNORedistributeProfitAsync));

            // staking
            WorkFlows.Add(BrokerActions.BRK_STK_CRSTK, (true, BrokerOperations.CNOCreateStakingAccountAsync, null));
            WorkFlows.Add(BrokerActions.BRK_STK_ADDSTK, (false, BrokerOperations.CNOAddStakingAsync, null));
            WorkFlows.Add(BrokerActions.BRK_STK_UNSTK, (true, BrokerOperations.CNOUnStakeAsync, null));

            // merchant
            WorkFlows.Add(BrokerActions.BRK_MCT_CRMCT, (true, BrokerOperations.CNOMCTCreateAsync, null));
            WorkFlows.Add(BrokerActions.BRK_MCT_PAYMCT, (false, BrokerOperations.CNOMCTPayAsync, null));
            WorkFlows.Add(BrokerActions.BRK_MCT_UNPAY, (true, BrokerOperations.CNOMCTUnPayAsync, null));
            WorkFlows.Add(BrokerActions.BRK_MCT_CFPAY, (true, BrokerOperations.CNOMCTConfirmPayAsync, null));
            WorkFlows.Add(BrokerActions.BRK_MCT_GETPAY, (true, BrokerOperations.CNOMCTGetPayAsync, null));
        }

        public static void CreateBlueprint(BrokerBlueprint blueprint)
        {
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
                Bps.TryRemove(hash, out _);
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

        public static void Persist(IAccountCollectionAsync stor)
        {
            // save to database
            var bps = stor.GetAllBlueprints();
            foreach (var bp in bps)
                stor.RemoveBlueprint(bp.svcReqHash);

            foreach (var x in Bps.Values)
                stor.CreateBlueprint(x);
        }
    }
}
