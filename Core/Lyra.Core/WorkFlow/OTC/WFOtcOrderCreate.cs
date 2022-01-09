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
    public class WFOtcOrderCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_CRODR,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new[]
                {
                    SendTokenFromDaoToOrderAsync,
                    CreateGenesisAsync
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

            OTCOrder order;
            try
            {
                order = JsonConvert.DeserializeObject<OTCOrder>(send.Tags["data"]);
            }
            catch (Exception ex)
            {
                return APIResultCodes.InvalidBlockTags;
            }

            var dao = await sys.Storage.FindLatestBlockAsync(order.daoid);
            if (dao == null || (dao as TransactionBlock).AccountID != send.DestinationAccountId)
                return APIResultCodes.InvalidOrgnization;

            // check every field of Order


            // verify collateral
            var chgs = send.GetBalanceChanges(last);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ||
                chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] < order.sellerCollateral)
                return APIResultCodes.InvalidCollateral;

            // TODO: check the price of order and collateral.

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> SendTokenFromDaoToOrderAsync(DagSystem sys, SendTransferBlock send)
        {
            var order = JsonConvert.DeserializeObject<OTCOrder>(send.Tags["data"]);

            var lastblock = await sys.Storage.FindLatestBlockAsync(order.daoid) as TransactionBlock;

            var keyStr = $"{send.Hash.Substring(0, 16)},{order.crypto},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendToOrderBlock = new DaoSendBlock
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

                // dao
                SellerCollateralPercentage = ((IDao)lastblock).SellerCollateralPercentage,
                ByerCollateralPercentage = ((IDao)lastblock).ByerCollateralPercentage,
                MetaHash = ((IDao)lastblock).MetaHash,
                Treasure = ((IDao)lastblock).Treasure.ToDecimalDict().ToLongDict(),
            };

            // calculate balance
            var dict = lastblock.Balances.ToDecimalDict();
            dict[order.crypto] -= order.amount;
            sendToOrderBlock.Balances = dict.ToLongDict();

            sendToOrderBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            sendToOrderBlock.InitializeBlock(lastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return sendToOrderBlock;
        }

        async Task<TransactionBlock> CreateGenesisAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var order = JsonConvert.DeserializeObject<OTCOrder>(send.Tags["data"]);

            var keyStr = $"{send.Hash.Substring(0, 16)},{order.crypto},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var otcblock = new OtcOrderGenesis
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = AccountTypes.OTC,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),

                // recv
                SourceHash = (blocks.First() as TransactionBlock).Hash,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // otc
                Order = order,
            };

            otcblock.Balances.Add(order.crypto, order.amount.ToBalanceLong());

            otcblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            otcblock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return otcblock;
        }
    }
}
