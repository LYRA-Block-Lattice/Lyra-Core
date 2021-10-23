using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Shared;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
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

        // main step, broker operation, can be null
        public bool mainDone { get; set; }

        // extra step
        public bool extraDone { get; set; }

        public bool FullDone => preDone && mainDone && extraDone;

        public async Task<bool> ExecuteAsync(DagSystem sys, SendTransferBlock send, Func<TransactionBlock, Task<(ConsensusResult?, APIResultCodes errorCode)>> submit)
        {
            // execute work flow
            var wf = BrokerFactory.WorkFlows[action];

            if (!preDone)
            {
                if(wf.pfrecv)
                {
                    var preBlock = await BrokerOperations.ReceivePoolFactoryFeeAsync(sys, send);
                    if (preBlock == null)
                        preDone = true;
                    else
                    {
                        var result = await submit(preBlock);
                        preDone = result.Item1 == ConsensusResult.Yea;
                        Console.WriteLine($"WF: {send.Hash.Shorten()} preDone: {preDone}");
                    }
                }
                else
                {
                    preDone = true;
                }
            }

            if(!mainDone && wf.brokerOps != null)
            {
                if(wf.brokerOps != null)
                {
                    var mainBlock = await wf.brokerOps(sys, send);
                    if (mainBlock == null)
                        mainDone = true;
                    else
                    {
                        // send it
                        var result2 = await submit(mainBlock);
                        mainDone = result2.Item1 == ConsensusResult.Yea;
                        Console.WriteLine($"WF: {send.Hash.Shorten()} {mainBlock.BlockType} mainDone: {mainDone}");
                    }
                }
                else
                {
                    mainDone = true;
                }
            }

            if (!extraDone && wf.extraOps != null)
            {
                var otherBlocks = await wf.extraOps(sys, svcReqHash);
                if(otherBlocks == null)
                {
                    extraDone = true;
                }
                else
                {
                    // foreach block send it
                    bool r3 = true;
                    foreach (var b in otherBlocks)
                    {
                        var result3 = await submit(b);
                        r3 = r3 && result3.Item1 == ConsensusResult.Yea;
                        Console.WriteLine($"WF: {send.Hash.Shorten()} {b.BlockType}: extraDone: {result3.Item1 == ConsensusResult.Yea}");
                    }
                    extraDone = r3;
                }
            }
            return FullDone;
        }
    }

    public class BrokerFactory
    {
        public static Dictionary<string, (bool pfrecv, Func<DagSystem, SendTransferBlock, Task<TransactionBlock>> brokerOps, Func<DagSystem, string, Task<List<TransactionBlock>>> extraOps)> WorkFlows { get; set; }

        public void Init()
        {
            if (WorkFlows != null)
                throw new InvalidOperationException("Already initialized.");

            WorkFlows = new Dictionary<string, (bool pfrecv, Func<DagSystem, SendTransferBlock, Task<TransactionBlock>> brokerOps, Func<DagSystem, string, Task<List<TransactionBlock>>> extraOps)>();

            // liquidate pool
            WorkFlows.Add(BrokerActions.BRK_POOL_CRPL, (true, BrokerOperations.CNOCreateLiquidatePoolAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_ADDLQ, (false, BrokerOperations.AddPoolLiquidateAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_RMLQ, (true, BrokerOperations.SendWithdrawFundsAsync, null));
            WorkFlows.Add(BrokerActions.BRK_POOL_SWAP, (false, BrokerOperations.ReceivePoolSwapInAsync, BrokerOperations.SendPoolSwapOutTokenAsync));

            // profiting
            WorkFlows.Add(BrokerActions.BRK_PFT_CRPFT, (true, BrokerOperations.CNOCreateProfitingAccountAsync, null));
            WorkFlows.Add(BrokerActions.BRK_PFT_GETPFT, (false, BrokerOperations.CNOReceiveProfitAsync, BrokerOperations.CNORedistributeProfitAsync));

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
    }
}
