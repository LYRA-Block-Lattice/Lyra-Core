using Lyra.Core.API;
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

namespace Lyra.Core.WorkFlow.DAO
{
    [LyraWorkFlow]
    public class WFVote : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_VOT_VOTE,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new[] { VoteAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 3 ||
                !send.Tags.ContainsKey("voteid") ||
                string.IsNullOrWhiteSpace(send.Tags["voteid"]) ||
                !send.Tags.ContainsKey("index") ||
                string.IsNullOrWhiteSpace(send.Tags["index"])
                )
                return APIResultCodes.InvalidBlockTags;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> VoteAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var voteid = send.Tags["voteid"];
            var index = int.Parse(send.Tags["index"]);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(voteid) as TransactionBlock;
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var gens = new VotingBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountID = prevBlock.AccountID,
                Balances = new Dictionary<string, long>(),

                // recv
                SourceHash = (blocks.Last() as TransactionBlock).Hash,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // voting
                OptionIndex = index,
            };

            gens.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            gens.InitializeBlock(prevBlock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return gens;
        }

    }
}
