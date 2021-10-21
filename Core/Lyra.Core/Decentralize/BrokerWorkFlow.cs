using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class BrokerWorkFlow
    {
        public bool pfrecv { get; set; }
        public Func<DagSystem, SendTransferBlock, Task<TransactionBlock>> brokerOps { get; set; }
        public Func<DagSystem, ReceiveTransferBlock, Task<List<TransactionBlock>>> extraOps { get; set; }

        public async Task<bool> ExecuteAsync(DagSystem sys, SendTransferBlock send, Func<TransactionBlock, Task<(ConsensusResult?, APIResultCodes errorCode)>> submit)
        {
            // execute work flow
            bool r1, r2, r3;
            if (pfrecv)
            {
                var pfrBlock = await BrokerOperations.ReceivePoolFactoryFeeAsync(sys, send);
                var result = await submit(pfrBlock);
                r1 = result.Item1 == ConsensusResult.Yea;
            }
            else
            {
                r1 = true;
            }
            var brkBlock = await brokerOps(sys, send);
            // send it
            var result2 = await submit(brkBlock);
            r2 = result2.Item1 == ConsensusResult.Yea;
            r3 = true;
            if (extraOps != null)
            {
                var otherBlocks = await extraOps(sys, brkBlock as ReceiveTransferBlock);
                // foreach block send it
                foreach (var b in otherBlocks)
                {
                    var result3 = await submit(b);
                    r3 = r3 && result3.Item1 == ConsensusResult.Yea;
                }
            }
            return r1 && r2 && r3;
        }
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
        public string relatedTx { get; set; }

        // work flow
        public string action { get; set; }
        public BrokerWorkFlow workflow { get; set; }
    }

    public class BrokerFactory
    {
        public Dictionary<string, (bool pfrecv, Func<DagSystem, SendTransferBlock, Task<TransactionBlock>> brokerOps, Func<DagSystem, ReceiveTransferBlock, Task<List<TransactionBlock>>> extraOps)> WorkFlows { get; set; }

        public void Init()
        {
            if (WorkFlows != null)
                throw new InvalidOperationException("Already initialized.");

            WorkFlows = new Dictionary<string, (bool pfrecv, Func<DagSystem, SendTransferBlock, Task<TransactionBlock>> brokerOps, Func<DagSystem, ReceiveTransferBlock, Task<List<TransactionBlock>>> extraOps)>();

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
