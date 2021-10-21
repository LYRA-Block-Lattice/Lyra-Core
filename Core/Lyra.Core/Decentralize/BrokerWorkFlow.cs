using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class BrokerWorkFlow
    {
        // properties
        public DateTime start { get; set; }
        public string initiatorAccount { get; set; }
        public string brokerAccount { get; set; }
        //public bool exclusive { get; set; }
        public string reqSendHash { get; set; }
        public string reqRecvHash { get; set; }

        // work flow
        public string action { get; set; }
        public bool pfrecv { get; set; }
        public Action<SendTransferBlock> brokerOps { get; set; }
        public Action<ReceiveTransferBlock> extraOps { get; set; }
    }

    public class BrokerFactory
    {
        public static Dictionary<string, (bool pfrecv, Func<DagSystem, SendTransferBlock, Task> brokerOps, Func<DagSystem, ReceiveTransferBlock, Task> extraOps)> WorkFlows;

        public void Init()
        {
            if (WorkFlows != null)
                throw new InvalidOperationException("Already initialized.");

            WorkFlows = new Dictionary<string, (bool pfrecv, Func<DagSystem, SendTransferBlock, Task> brokerOps, Func<DagSystem, ReceiveTransferBlock, Task> extraOps)>();

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
