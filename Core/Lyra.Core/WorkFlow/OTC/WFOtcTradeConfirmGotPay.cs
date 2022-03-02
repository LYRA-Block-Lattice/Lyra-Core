﻿using Lyra.Core.API;
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
    [LyraWorkFlow]
    public class WFOtcTradeConfirmGotPay : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_TRDPAYGOT,
                RecvVia = BrokerRecvType.None,
                Steps = new[] { ChangeStateAsync, SendCryptoProductToBuyerAsync, SendCollateralToBuyerAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("tradeid") ||
                string.IsNullOrWhiteSpace(send.Tags["tradeid"]))
                return APIResultCodes.InvalidBlockTags;

            var tradeid = send.Tags["tradeid"];
            var tradeblk = await sys.Storage.FindLatestBlockAsync(tradeid);
            if (tradeblk == null)
                return APIResultCodes.InvalidTrade;

            if ((tradeblk as IOtcTrade).OTStatus != OTCTradeStatus.FiatSent)
                return APIResultCodes.InvalidTradeStatus;

            // check if seller is the order's owner
            var orderid = (tradeblk as IOtcTrade).Trade.orderId;
            var orderblk = await sys.Storage.FindLatestBlockAsync(orderid);
            if (orderblk == null)
                return APIResultCodes.InvalidOrder;

            if ((orderblk as IBrokerAccount).OwnerAccountId != send.AccountID)
                return APIResultCodes.NotSellerOfTrade;

            return APIResultCodes.Success;
        }

        protected Task<TransactionBlock> SendCryptoProductToBuyerAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            return TradeBlockOperateAsync(sys, sendBlock,
                () => new OtcTradeSendBlock(),
                (b) =>
                {
                    (b as IOtcTrade).OTStatus = OTCTradeStatus.CryptoReleased;
                    (b as SendTransferBlock).DestinationAccountId = (b as IOtcTrade).OwnerAccountId;
                    (b as SendTransferBlock).Balances[(b as IOtcTrade).Trade.crypto] = 0;
                });
        }

        protected async Task<TransactionBlock> SendCollateralToBuyerAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            var tradelatest = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            var trade = (tradelatest as IOtcTrade).Trade;
            var daolastblock = await sys.Storage.FindLatestBlockAsync(trade.daoId) as TransactionBlock;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendCollateral = new DaoSendBlock
            {
                // block
                ServiceHash = sb.Hash,

                // trans
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                AccountID = daolastblock.AccountID,

                // send
                DestinationAccountId = (tradelatest as IBrokerAccount).OwnerAccountId,

                // broker
                Name = ((IBrokerAccount)daolastblock).Name,
                OwnerAccountId = ((IBrokerAccount)daolastblock).OwnerAccountId,
                RelatedTx = sendBlock.Hash,

                // profiting
                PType = ((IProfiting)daolastblock).PType,
                ShareRito = ((IProfiting)daolastblock).ShareRito,
                Seats = ((IProfiting)daolastblock).Seats,

                // dao
                SellerPar = ((IDao)daolastblock).SellerPar,
                BuyerPar = ((IDao)daolastblock).BuyerPar,
                Description = ((IDao)daolastblock).Description,
                Treasure = ((IDao)daolastblock).Treasure.ToDecimalDict().ToLongDict(),
            };

            // calculate balance
            var dict = daolastblock.Balances.ToDecimalDict();
            dict[LyraGlobal.OFFICIALTICKERCODE] -= trade.collateral;
            sendCollateral.Balances = dict.ToLongDict();

            sendCollateral.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            sendCollateral.InitializeBlock(daolastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return sendCollateral;
        }

        protected Task<TransactionBlock> ChangeStateAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            return TradeBlockOperateAsync(sys, sendBlock,
                () => new OtcTradeRecvBlock(),
                (b) =>
                {
                    var txInfo = sendBlock.GetBalanceChanges(sys.Storage.FindBlockByHash(sendBlock.PreviousHash) as TransactionBlock);

                    var recvBalances = b.Balances.ToDecimalDict();
                    foreach (var chg in txInfo.Changes)
                    {
                        if (recvBalances.ContainsKey(chg.Key))
                            recvBalances[chg.Key] += chg.Value;
                        else
                            recvBalances.Add(chg.Key, chg.Value);
                    }
                    b.Balances = recvBalances.ToLongDict();

                    (b as IOtcTrade).OTStatus = OTCTradeStatus.FiatReceived;
                });
        }

        protected async Task<TransactionBlock> TradeBlockOperateAsync(
            DagSystem sys, 
            SendTransferBlock sendBlock,
            Func<TransactionBlock> GenBlock,
            Action<TransactionBlock> ChangeBlock
            )
        {
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var nextblock = GenBlock();

            // block
            nextblock.ServiceHash = lsb.Hash;

            // transaction
            nextblock.AccountID = sendBlock.DestinationAccountId;
            nextblock.Balances = new Dictionary<string, long>();
            nextblock.Fee = 0;
            nextblock.FeeCode = LyraGlobal.OFFICIALTICKERCODE;
            nextblock.FeeType = AuthorizationFeeTypes.NoFee;

            // broker
            (nextblock as IBrokerAccount).Name = ((IBrokerAccount)lastblock).Name;
            (nextblock as IBrokerAccount).OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId;
            (nextblock as IBrokerAccount).RelatedTx = sendBlock.Hash;

            // trade     
            (nextblock as IOtcTrade).Trade = ((IOtcTrade)lastblock).Trade;
            (nextblock as IOtcTrade).OTStatus = ((IOtcTrade)lastblock).OTStatus;

            nextblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            var latestBalances = lastblock.Balances.ToDecimalDict();
            var recvBalances = lastblock.Balances.ToDecimalDict();
            nextblock.Balances = recvBalances.ToLongDict();

            ChangeBlock(nextblock);

            await nextblock.InitializeBlockAsync(lastblock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return nextblock;
        }
    }
}
