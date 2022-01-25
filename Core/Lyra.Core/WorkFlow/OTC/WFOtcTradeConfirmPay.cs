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
    [LyraWorkFlow]
    public class WFOtcTradeConfirmPay : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_TRDPAYSENT,
                RecvVia = BrokerRecvType.None,
                Steps = new [] { ChangeStateAsync }
            };
        }

        // user pay via off-chain ways and confirm payment in OTC trade.
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
            
            if ((tradeblk as IBrokerAccount).OwnerAccountId != send.AccountID)
                return APIResultCodes.NotOwnerOfTrade;

            if ((tradeblk as IOtcTrade).OTStatus != OTCTradeStatus.Open)
                return APIResultCodes.InvalidTradeStatus;

            return APIResultCodes.Success;
        }

        protected async Task<TransactionBlock> ChangeStateAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            var txInfo = sendBlock.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock);
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var receiveBlock = new OtcTradeRecvBlock
            {
                // block
                ServiceHash = lsb.Hash,

                // transaction
                AccountID = sendBlock.DestinationAccountId,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // broker
                Name = ((IBrokerAccount)lastblock).Name,
                OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId,
                RelatedTx = sendBlock.Hash,

                // trade     
                Trade = ((IOtcTrade)lastblock).Trade,
                OTStatus = OTCTradeStatus.FiatSent,
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            var latestBalances = lastblock.Balances.ToDecimalDict();
            var recvBalances = lastblock.Balances.ToDecimalDict();
            foreach (var chg in txInfo.Changes)
            {
                if (recvBalances.ContainsKey(chg.Key))
                    recvBalances[chg.Key] += chg.Value;
                else
                    recvBalances.Add(chg.Key, chg.Value);
            }

            receiveBlock.Balances = recvBalances.ToLongDict();

            await receiveBlock.InitializeBlockAsync(lastblock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return receiveBlock;
        }
    }
}
