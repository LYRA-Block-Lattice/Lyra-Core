using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow
{
    [LyraWorkFlow]
    public class WFOtcTradeCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_CRTRD,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new[]
                {
                    SendTokenFromOrderToTradeAsync,
                    TradeGenesisReceiveAsync
                }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("data") ||
                string.IsNullOrWhiteSpace(send.Tags["data"])
                )
                return APIResultCodes.InvalidBlockTags;

            OTCTrade trade;
            try
            {
                trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);
            }
            catch (Exception ex)
            {
                return APIResultCodes.InvalidBlockTags;
            }

            var dao = await sys.Storage.FindLatestBlockAsync(trade.daoid);
            if (dao == null || (dao as TransactionBlock).AccountID != send.DestinationAccountId)
                return APIResultCodes.InvalidOrgnization;

            var orderblk = await sys.Storage.FindLatestBlockAsync(trade.orderid) as IOtcOrder;
            if (orderblk == null)
                return APIResultCodes.InvalidOrder;

            var order = orderblk.Order;
            if (order.crypto != trade.crypto ||
                order.fiat != trade.fiat ||
                order.amount <= trade.amount ||
                order.dir == trade.dir
                )
                return APIResultCodes.InvalidTrade;

            // verify collateral
            var chgs = send.GetBalanceChanges(last);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ||
                chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] < trade.collateral)
                return APIResultCodes.InvalidCollateral;

            // TODO: check the price of order and collateral.

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> SendTokenFromOrderToTradeAsync(DagSystem sys, SendTransferBlock send)
        {
            var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);

            var lastblock = await sys.Storage.FindLatestBlockAsync(trade.orderid) as TransactionBlock;

            var keyStr = $"{send.Hash.Substring(0, 16)},{trade.crypto},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendToTradeBlock = new OtcOrderSendBlock
            {
                // block
                ServiceHash = sb.Hash,

                // trans
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                AccountID = lastblock.AccountID,

                // send
                DestinationAccountId = AccountId,

                // broker
                Name = ((IBrokerAccount)lastblock).Name,
                OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId,
                RelatedTx = send.Hash,

                // otc
                Order = new OTCOrder
                {
                    daoid = ((IOtcOrder)lastblock).Order.daoid,
                    dir = ((IOtcOrder)lastblock).Order.dir,
                    crypto = ((IOtcOrder)lastblock).Order.crypto,
                    fiat = ((IOtcOrder)lastblock).Order.fiat,
                    priceType = ((IOtcOrder)lastblock).Order.priceType,
                    price = ((IOtcOrder)lastblock).Order.price,
                    amount = ((IOtcOrder)lastblock).Order.amount - trade.amount,
                }
            };

            // calculate balance
            var dict = lastblock.Balances.ToDecimalDict();
            dict[trade.crypto] -= trade.amount;
            sendToTradeBlock.Balances = dict.ToLongDict();

            sendToTradeBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            sendToTradeBlock.InitializeBlock(lastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return sendToTradeBlock;
        }

        async Task<TransactionBlock> TradeGenesisReceiveAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);

            var keyStr = $"{send.Hash.Substring(0, 16)},{trade.crypto},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var otcblock = new OtcTradeGenesisBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = AccountTypes.OTC,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),

                // receive
                SourceHash = (blocks.First() as TransactionBlock).Hash,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // otc
                Trade = trade,
            };

            otcblock.Balances.Add(trade.crypto, trade.amount.ToBalanceLong());
            otcblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            otcblock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return otcblock;
        }

        //async Task<TransactionBlock> SendUtilityTokenToUserAsync(DagSystem sys, SendTransferBlock send)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
