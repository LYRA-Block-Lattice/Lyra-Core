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
                Blocks = new[]{
                    new BlockDesc
                    {
                        BlockType = BlockTypes.OTCTradeGenesis,
                        TheBlock = typeof(OtcTradeGenesis),
                        //AuthorizerType = typeof(OtcGenesisAuthorizer),
                    }
                }
            };
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            throw new NotImplementedException();
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var ogen = blocks.FirstOrDefault(a => a is OtcOrderGenesis);
            if (ogen != null)
                return null;

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

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // otc
                Order = order,
            };

            otcblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            otcblock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return otcblock;
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);
            var dao = await sys.Storage.FindFirstBlockAsync(trade.daoid) as DaoGenesisBlock;
            if (dao == null || dao.AccountID != send.DestinationAccountId)
                return APIResultCodes.InvalidOrgnization;

            var orderblk = sys.Storage.GetOtcOrderByID(trade.orderid);
            if (orderblk == null)
                return APIResultCodes.InvalidOrder;

            var order = orderblk.Order;
            if (order.crypto != trade.crypto ||
                order.fiat != trade.fiat ||
                order.amount <= trade.amount)
                return APIResultCodes.InvalidTrade;

            return APIResultCodes.Success;
        }
    }
}
