using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.Identity;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Neo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.Uni
{
    [LyraWorkFlow]//v
    public class WFUniTradeCancel : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_UNI_TRDCANCEL,
                RecvVia = BrokerRecvType.None,
            };
        }

        public async override Task<Func<DagSystem, LyraContext, Task<TransactionBlock>>[]> GetProceduresAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var tradeid = send.Tags["tradeid"];
            var trade = await sys.Storage.FindLatestBlockAsync(tradeid) as IUniTrade;

            return new[] {
                    SealTradeAsync,
                    SendTokenFromTradeToOrderAsync,
                    OrderReceiveTokenFromTradeAsync,
                    SendCollateralToBuyerAsync
            };
            
            //}
            //else
            //{
            //    //todo: sell trade should send token to trade owner.
            //    return new[] {
            //        SealTradeAsync,
            //        SendTokenFromTradeToTradeOwnerAsync,
            //        SendCollateralToBuyerAsync
            //    };
            //}
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags.Count != 4 ||
                !send.Tags.ContainsKey("tradeid") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"]) ||
                !send.Tags.ContainsKey("orderid") ||
                string.IsNullOrWhiteSpace(send.Tags["orderid"]) ||
                !send.Tags.ContainsKey("daoid") ||
                string.IsNullOrWhiteSpace(send.Tags["daoid"])
                )
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var orderid = send.Tags["orderid"];
            var daoid = send.Tags["daoid"];

            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid) as IUniTrade;
            var daoblk = await sys.Storage.FindLatestBlockAsync(tradeblk.Trade.daoId) as IDao;
            if (daoblk == null || tradeblk == null || daoblk.AccountID != daoid ||
                tradeblk.Trade.daoId != daoblk.AccountID)
                return APIResultCodes.InvalidTrade;

            var dlr = sys.Storage.FindFirstBlock(tradeblk.Trade.dealerId) as IDealer;
            if (dlr == null)
                return APIResultCodes.InvalidDealerServer;

            if (tradeblk.OwnerAccountId != send.AccountID 
                && tradeblk.Trade.orderOwnerId != send.AccountID
                && send.AccountID != dlr.OwnerAccountId)    // dealer owner can cancel the trade
                return APIResultCodes.InvalidTrade;

            if (tradeblk.UTStatus != UniTradeStatus.Open)
                return APIResultCodes.InvalidOperation;

            var orderblk = await sys.Storage.FindLatestBlockAsync(orderid) as IUniOrder;
            if(orderblk == null || tradeblk.Trade.orderId != orderid
                || tradeblk.Trade.daoId != daoid)
            {
                return APIResultCodes.InvalidTrade;
            }

            if(Settings.Default.LyraNode.Lyra.NetworkId != "xtest" && !string.IsNullOrEmpty(tradeblk.Trade.dealerId))
            {
                // check if trade is cancellable
                var lsb = sys.Storage.GetLastServiceBlock();
                var wallet = sys.PosWallet;
                var sign = Signatures.GetSignature(wallet.PrivateKey, lsb.Hash, wallet.AccountId);
                var dlrblk = await sys.Storage.FindLatestBlockAsync(tradeblk.Trade.dealerId);
                var uri = new Uri(new Uri((dlrblk as IDealer).Endpoint), "/api/dealer/");
                var dealer = new DealerClient(uri);
                var ret = await dealer.GetTradeBriefAsync(tradeid, wallet.AccountId, sign);
                if (!ret.Successful())
                    return APIResultCodes.InvalidOperation;

                var brief = ret.Deserialize<TradeBrief>();
                if (brief == null || !brief.IsCancellable)
                    return APIResultCodes.InvalidOperation;

                if (send.AccountID == dlr.OwnerAccountId)
                {
                    // dealer do cancel. we also authorize this action to prevent abuse.
                    // we verify cancel request and reply by signature.
                    var lastCase = brief.GetDisputeHistory().Last();

                    // TODO: recovery it.
                    //if (!lastCase.Verify(tradeblk))
                    //    return APIResultCodes.Unauthorized;

                    //if (!lastCase.GetAllowCancel())
                    //    return APIResultCodes.InvalidOperation; // cancellation is controlled by trading room

                    //if (lastCase.Complaint.request != ComplaintRequest.CancelTrade || lastCase.Reply.response != ComplaintResponse.AgreeCancel)
                    //    return APIResultCodes.Unauthorized;

                    //if(lastCase.Complaint.ownerId == tradeblk.OwnerAccountId && lastCase.Reply.ownerId != orderblk.OwnerAccountId)   // guest
                    //{
                    //    return APIResultCodes.Unauthorized;
                    //}

                    //if (lastCase.Complaint.ownerId == orderblk.OwnerAccountId && lastCase.Reply.ownerId != tradeblk.OwnerAccountId)   // host
                    //{
                    //    return APIResultCodes.Unauthorized;
                    //}
                }
            }

            return APIResultCodes.Success;
        }

        public override async Task<ReceiveTransferBlock?> NormalReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await SealTradeAsync(sys, context) as ReceiveTransferBlock;
        }

        public override async Task<ReceiveTransferBlock?> RefundReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await SealTradeAsync(sys, context) as ReceiveTransferBlock;
        }

        async Task<TransactionBlock> SealTradeAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var tradeid = send.Tags["tradeid"];

            var lastblock = await sys.Storage.FindLatestBlockAsync(tradeid) as TransactionBlock;

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniTradeRecvBlock>(),
                () => context.State,
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    if (oldbalance.ContainsKey("LYR"))
                        oldbalance["LYR"] += txInfo.Changes["LYR"];
                    else
                        oldbalance.Add("LYR", txInfo.Changes["LYR"]);
                    b.Balances = oldbalance.ToLongDict();

                    // Trade status
                    (b as IUniTrade).UTStatus = UniTradeStatus.Canceled;

                    // if refund receive, attach a refund reason.
                    if (context.State == WFState.NormalReceive || context.State == WFState.RefundReceive)
                    {
                        b.AddTag("auth", context.AuthResult.Result.ToString());
                    }
                });
        }

        async Task<TransactionBlock> SendTokenFromTradeToOrderAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var tradeid = send.Tags["tradeid"];

            var lastblock = await sys.Storage.FindLatestBlockAsync(tradeid) as TransactionBlock;
            var tradeblk = lastblock as IUniTrade;

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // send
                    (b as SendTransferBlock).DestinationAccountId = (lastblock as IUniTrade).Trade.orderId;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();

                    oldbalance[tradeblk.Trade.offering] = 0;
                    oldbalance["LYR"] -= tradeblk.OdrCltMmt.ToBalanceDecimal();
                    
                    b.Balances = oldbalance.ToLongDict();
                });
        }

        async Task<TransactionBlock> OrderReceiveTokenFromTradeAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var tradeid = send.Tags["tradeid"];

            var lastblocktrade = await sys.Storage.FindLatestBlockAsync(tradeid) as TransactionBlock;
            var lastblockorder = await sys.Storage.FindLatestBlockAsync((lastblocktrade as IUniTrade).Trade.orderId) as TransactionBlock;

            var txInfo = lastblocktrade.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(lastblocktrade.PreviousHash) as TransactionBlock);

            return await TransactionOperateAsync(sys, send.Hash, lastblockorder,
                () => lastblockorder.GenInc<UniOrderRecvBlock>(),
                () => context.State,
                (b) =>
                {
                    // send
                    (b as ReceiveTransferBlock).SourceHash = lastblocktrade.Hash;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    foreach (var chg in txInfo.Changes)
                    {
                        if(oldbalance.ContainsKey(chg.Key))
                            oldbalance[chg.Key] += chg.Value;
                        else
                            oldbalance[chg.Key] = chg.Value;
                    }
                    b.Balances = oldbalance.ToLongDict();
                });
        }

        protected async Task<TransactionBlock> SendCollateralToBuyerAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var tradeid = send.Tags["tradeid"];
            var tradelatest = await sys.Storage.FindLatestBlockAsync(tradeid) as TransactionBlock;

            var tradeblk = tradelatest as IUniTrade;
            var daolastblock = await sys.Storage.FindLatestBlockAsync(tradeblk.Trade.daoId) as TransactionBlock;

            return await TransactionOperateAsync(sys, send.Hash, daolastblock,
                () => daolastblock.GenInc<DaoSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // recv
                    (b as SendTransferBlock).DestinationAccountId = send.AccountID;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    oldbalance[tradeblk.Trade.biding] -= tradeblk.Trade.pay;
                    oldbalance["LYR"] -= tradeblk.Trade.cltamt;
                    b.Balances = oldbalance.ToLongDict();
                });
        }

        async Task<TransactionBlock> SendTokenFromTradeToTradeOwnerAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var tradeid = send.Tags["tradeid"];
            var trade = await sys.Storage.FindLatestBlockAsync(tradeid) as IUniTrade;

            var lastblock = await sys.Storage.FindLatestBlockAsync(tradeid) as TransactionBlock;

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            return await TransactionOperateAsync(sys, send.Hash, lastblock,
                () => lastblock.GenInc<UniTradeSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // send
                    (b as SendTransferBlock).DestinationAccountId = (lastblock as IUniTrade).OwnerAccountId;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // balance
                    var oldbalance = b.Balances.ToDecimalDict();
                    oldbalance[trade.Trade.biding] = 0;
                    b.Balances = oldbalance.ToLongDict();
                });
        }
    }
}
