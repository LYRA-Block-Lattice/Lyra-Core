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
                RecvVia = BrokerRecvType.None,
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

            var voteid = send.Tags["voteid"];
            var index = int.Parse(send.Tags["index"]);

            var voteg = sys.Storage.FindFirstBlock(voteid) as VotingGenesisBlock;
            var votel = await sys.Storage.FindLatestBlockAsync(voteid);

            // index must in range
            if(index < 0 || index > voteg.Subject.Options.Count() - 1)
            {
                return APIResultCodes.InvalidVote;
            }

            // voter should in treasure
            var dao = await sys.Storage.FindLatestBlockAsync(voteg.Subject.DaoId);
            if(!(dao as IDao).Treasure.ContainsKey(send.AccountID))
            {
                return APIResultCodes.InvalidVote;
            }

            // voter shouldn't multiple vote
            for(var a = votel.Height; a > 1; a--)
            {
                var vx = await sys.Storage.FindBlockByHeightAsync(voteid, a);
                if ((vx as VotingBlock).OwnerAccountId == send.AccountID)
                    return APIResultCodes.InvalidVote;
            }

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> VoteAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var voteid = send.Tags["voteid"];
            var index = int.Parse(send.Tags["index"]);

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

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
                SourceHash = send.Hash,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // voting
                OptionIndex = index,
            };

            var oldbalance = prevBlock.Balances.ToDecimalDict();
            if (oldbalance.ContainsKey("LYR"))
                oldbalance["LYR"] += txInfo.Changes["LYR"];
            else
                oldbalance.Add("LYR", txInfo.Changes["LYR"]);
            gens.Balances = oldbalance.ToLongDict();

            gens.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            gens.InitializeBlock(prevBlock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return gens;
        }

    }
}
