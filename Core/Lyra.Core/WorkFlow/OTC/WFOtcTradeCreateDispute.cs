using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.OTC
{
    [LyraWorkFlow]//v
    public class WFOtcTradeCreateDispute : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_CRDPT,
                RecvVia = BrokerRecvType.None,
                Steps = new [] { ChangeStateAsync }
            };
        }

        // user pay via off-chain ways and confirm payment in OTC trade.
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 1)
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.DestinationAccountId;
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null || tradeblk is not IOtcTrade)
                return APIResultCodes.InvalidTrade;
            
            if ((tradeblk as IBrokerAccount).OwnerAccountId != send.AccountID &&
                (tradeblk as IOtcTrade).Trade.orderOwnerId != send.AccountID
                )
                return APIResultCodes.NotOwnerOfTrade;

            // can't reopen closed dispute trade
            if ((tradeblk as IOtcTrade).OTStatus == OTCTradeStatus.Dispute ||
                (tradeblk as IOtcTrade).OTStatus == OTCTradeStatus.DisputeClosed)
                return APIResultCodes.InvalidTradeStatus;

            return APIResultCodes.Success;
        }

        protected async Task<TransactionBlock> ChangeStateAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId) as TransactionBlock;
            var votblk = await TransactionOperateAsync(sys, send.Hash, prevBlock,
                () => prevBlock.GenInc<OtcTradeRecvBlock>(),
                () => WFState.Finished,
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // trade
                    (b as IOtcTrade).OTStatus = OTCTradeStatus.Dispute;

                    var oldbalance = prevBlock.Balances.ToDecimalDict();
                    if (oldbalance.ContainsKey("LYR"))
                        oldbalance["LYR"] += txInfo.Changes["LYR"];
                    else
                        oldbalance.Add("LYR", txInfo.Changes["LYR"]);
                    b.Balances = oldbalance.ToLongDict();
                });
            return votblk;
        }
    }
}
