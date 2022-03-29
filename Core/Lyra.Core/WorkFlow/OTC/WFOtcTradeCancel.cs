using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.Identity;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Neo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.OTC
{
    [LyraWorkFlow]
    public class WFOtcTradeCancel : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_TRDCANCEL,
                RecvVia = BrokerRecvType.None,
                Steps = new [] { SealTradeAsync, SendCollateralToBuyerAsync}
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("tradeid") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"])
                )
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            var daoblk = await sys.Storage.FindLatestBlockAsync((tradeblk as IOtcTrade).Trade.daoId);
            if (daoblk == null || tradeblk == null || 
                (tradeblk as IOtcTrade).Trade.daoId != (daoblk as TransactionBlock).AccountID)
                return APIResultCodes.InvalidTrade;

            if ((tradeblk as IBrokerAccount).OwnerAccountId != send.AccountID)
                return APIResultCodes.InvalidTrade;

            if ((tradeblk as IOtcTrade).OTStatus != OTCTradeStatus.Open)
                return APIResultCodes.InvalidTrade;

            if(Settings.Default.LyraNode.Lyra.NetworkId != "xtest")
            {
                // check if trade is cancellable
                var lsb = sys.Storage.GetLastServiceBlock();
                var wallet = sys.PosWallet;
                var sign = Signatures.GetSignature(wallet.PrivateKey, lsb.Hash, wallet.AccountId);
                var dealer = new DealerClient(sys.PosWallet.NetworkId);
                var ret = await dealer.GetTradeBriefAsync(tradeid, wallet.AccountId, sign);
                if (!ret.Successful() || !ret.Deserialize<TradeBrief>().IsCancellable)
                    return APIResultCodes.InvalidOperation;
            }

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> SealTradeAsync(DagSystem sys, SendTransferBlock send)
        {
            var tradeid = send.Tags["tradeid"];

            var lastblock = await sys.Storage.FindLatestBlockAsync(tradeid) as TransactionBlock;

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<OtcTradeRecvBlock>(),
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;

                    // broker
                    (b as IBrokerAccount).OwnerAccountId = send.AccountID;
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    if (oldbalance.ContainsKey("LYR"))
                        oldbalance["LYR"] += txInfo.Changes["LYR"];
                    else
                        oldbalance.Add("LYR", txInfo.Changes["LYR"]);
                    b.Balances = oldbalance.ToLongDict();

                    // Trade status
                    (b as IOtcTrade).OTStatus = OTCTradeStatus.Canceled;
                });
        }

        protected async Task<TransactionBlock> SendCollateralToBuyerAsync(DagSystem sys, SendTransferBlock send)
        {
            var tradeid = send.Tags["tradeid"];
            var tradelatest = await sys.Storage.FindLatestBlockAsync(tradeid) as TransactionBlock;

            var tradeblk = tradelatest as IOtcTrade;
            var daolastblock = await sys.Storage.FindLatestBlockAsync(tradeblk.Trade.daoId) as TransactionBlock;

            var sb = await sys.Storage.GetLastServiceBlockAsync();

            return await TransactionOperateAsync(sys, send.Hash, daolastblock,
                () => daolastblock.GenInc<DaoSendBlock>(),
                (b) =>
                {
                    // recv
                    (b as SendTransferBlock).DestinationAccountId = send.AccountID;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    oldbalance["LYR"] -= tradeblk.Trade.collateral;
                    b.Balances = oldbalance.ToLongDict();
                });
        }
    }
}
