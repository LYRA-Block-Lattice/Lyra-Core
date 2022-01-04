using Lyra.Core.Accounts;
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
            var wf = BrokerFactory.WorkFlows[action];
            var send = await sys.Storage.FindBlockByHashAsync(svcReqHash) as SendTransferBlock;
            if (send == null)
                return false;

            if (!preDone)
            {
                if(wf.pfrecv)
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
                else
                {
                    preDone = true;
                }
            }

            if(preDone && !mainDone)
            {
                if(wf.brokerOps != null)
                {
                    var mainBlock = await wf.brokerOps(sys, send);
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
                else
                {
                    mainDone = true;
                }
            }

            if (preDone && mainDone && !extraDone)
            {
                if(wf.extraOps != null)
                {
                    var otherBlocks = await wf.extraOps(sys, svcReqHash);
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
        public static Dictionary<string, (Func<DagSystem, SendTransferBlock, TransactionBlock, Task<APIResultCodes>> preSendAuth, bool pfrecv, Func<DagSystem, SendTransferBlock, Task<TransactionBlock>> brokerOps, Func<DagSystem, string, Task<TransactionBlock>> extraOps)> WorkFlows { get; set; }

        public static ConcurrentDictionary<string, BrokerBlueprint> Bps { get; set; }

        public static event Action<BrokerBlueprint> OnFinished;
        public void Init()
        {
            if (WorkFlows != null)
                throw new InvalidOperationException("Already initialized.");

            WorkFlows = new Dictionary<string, (
                Func<DagSystem, SendTransferBlock, TransactionBlock, Task<APIResultCodes>> preSendAuth, 
                bool pfrecv, 
                Func<DagSystem, SendTransferBlock, Task<TransactionBlock>> brokerOps, 
                Func<DagSystem, string, Task<TransactionBlock>> extraOps
            )>();
            Bps = new ConcurrentDictionary<string, BrokerBlueprint>();

            // liquidate pool
            WorkFlows.Add(BrokerActions.BRK_POOL_CRPL, (WFPool.CNOCreateLiquidatePoolPreAuthAsync, true, WFPool.CNOCreateLiquidatePoolAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_ADDLQ, (WFPool.VerifyAddLiquidateToPoolAsync, false, WFPool.AddPoolLiquidateAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_RMLQ, (WFPool.VerifyWithdrawFromPoolAsync, true, WFPool.SendWithdrawFundsAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_SWAP, (WFPool.VerifyPoolSwapAsync, false, WFPool.ReceivePoolSwapInAsync, WFPool.SendPoolSwapOutTokenAsync));

            // profiting
            WorkFlows.Add(BrokerActions.BRK_PFT_CRPFT, (WFProfit.VerifyStkPftAsync, true, WFProfit.CNOCreateProfitingAccountAsync, null));
            //WorkFlows.Add(BrokerActions.BRK_PFT_FEEPFT, (true, BrokerOperations.SyncNodeFeesAsync, null));
            WorkFlows.Add(BrokerActions.BRK_PFT_GETPFT, (WFProfit.VerifyStkPftAsync, true, WFProfit.CNOReceiveAllProfitAsync, WFProfit.CNORedistributeProfitAsync));

            // staking
            WorkFlows.Add(BrokerActions.BRK_STK_CRSTK, (WFProfit.VerifyStkPftAsync, true, WFStaking.CNOCreateStakingAccountAsync, null));
            WorkFlows.Add(BrokerActions.BRK_STK_ADDSTK, (WFProfit.VerifyStkPftAsync, false, WFStaking.CNOAddStakingAsync, null));
            WorkFlows.Add(BrokerActions.BRK_STK_UNSTK, (WFProfit.VerifyStkPftAsync, true, WFStaking.CNOUnStakeAsync, null));

            // DEX
            WorkFlows.Add(BrokerActions.BRK_DEX_DPOREQ, (WFDex.CNODepositPreAuthAsync, true, WFDex.CNODEXCreateWalletAsync, null));
            WorkFlows.Add(BrokerActions.BRK_DEX_MINT, (WFDex.CNOMintTokenPreAuthAsync, true, WFDex.CNODEXMintTokenAsync, null));
            WorkFlows.Add(BrokerActions.BRK_DEX_GETTKN, (WFDex.CNOGetTokenPreAuthAsync, true, WFDex.CNODEXGetTokenAsync, null));
            WorkFlows.Add(BrokerActions.BRK_DEX_PUTTKN, (WFDex.CNOPutTokenPreAuthAsync, false, WFDex.CNODEXPutTokenAsync, null));
            WorkFlows.Add(BrokerActions.BRK_DEX_WDWREQ, (WFDex.CNOWithdrawReqPreAuthAsync, true, WFDex.CNODEXWithdrawAsync, WFDex.CNODEXWithdrawToExtBlockchainReqAsync));

            // merchant
            //WorkFlows.Add(BrokerActions.BRK_MCT_CRMCT, (true, BrokerOperations.CNOMCTCreateAsync, null));
            //WorkFlows.Add(BrokerActions.BRK_MCT_PAYMCT, (false, BrokerOperations.CNOMCTPayAsync, null));
            //WorkFlows.Add(BrokerActions.BRK_MCT_UNPAY, (true, BrokerOperations.CNOMCTUnPayAsync, null));
            //WorkFlows.Add(BrokerActions.BRK_MCT_CFPAY, (true, BrokerOperations.CNOMCTConfirmPayAsync, null));
            //WorkFlows.Add(BrokerActions.BRK_MCT_GETPAY, (true, BrokerOperations.CNOMCTGetPayAsync, null));
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
