﻿using Converto;
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
    [LyraWorkFlow]//v
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

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
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

            // voter should in treasure the time when vote generated
            // we find it by voteg.relatedtx
            var vgreltx = voteg.RelatedTx;
            var allreltx = await sys.Storage.FindBlocksByRelatedTxAsync(vgreltx);
            var dao = await sys.Storage.FindBlockByHashAsync(
                allreltx.First(a => a is DaoRecvBlock).Hash);

            if((votel as IVoting).OwnerAccountId == LyraGlobal.GetLordAccountId(Neo.Settings.Default.LyraNode.Lyra.NetworkId))
            {
                // this is a vote create by lyra council via the Lord
                // so the voter should be in current view
                var lsb = sys.Storage.GetLastServiceBlock();
                if(!lsb.Authorizers.Keys.Contains(send.AccountID))
                    return APIResultCodes.Unauthorized;
            }
            else if(!(dao as IDao).Treasure.ContainsKey(send.AccountID))
            {
                return APIResultCodes.Unauthorized;
            }

            // voter shouldn't multiple vote
            for(var a = votel.Height; a > 1; a--)
            {
                var vx = await sys.Storage.FindBlockByIndexAsync(voteid, a);
                if ((vx as VotingBlock).VoterId == send.AccountID)
                    return APIResultCodes.InvalidVote;
            }

            // vote should be in progress
            if ((votel as IVoting).VoteState != VoteStatus.InProgress)
                return APIResultCodes.InvalidVote;

            return APIResultCodes.Success;
        }

        public override async Task<ReceiveTransferBlock?> NormalReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await VoteAsync(sys, context) as ReceiveTransferBlock;
        }

        public override async Task<ReceiveTransferBlock?> RefundReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await VoteAsync(sys, context) as ReceiveTransferBlock;
        }

        async Task<TransactionBlock?> VoteAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var voteid = send.Tags["voteid"];
            var index = int.Parse(send.Tags["index"]);

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(voteid) as TransactionBlock;
            var sb = await sys.Storage.GetLastServiceBlockAsync();

            var votblk = await TransactionOperateAsync(sys, send.Hash, prevBlock,
                () => prevBlock.GenInc<VotingBlock>(),
                () => context.State,
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;

                    // broker
                    (b as IBrokerAccount).OwnerAccountId = send.AccountID;
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // voting
                    (b as VotingBlock).VoterId = send.AccountID;
                    (b as VotingBlock).OptionIndex = index;

                    var oldbalance = prevBlock.Balances.ToDecimalDict();
                    if (oldbalance.ContainsKey("LYR"))
                        oldbalance["LYR"] += txInfo.Changes["LYR"];
                    else
                        oldbalance.Add("LYR", txInfo.Changes["LYR"]);
                    b.Balances = oldbalance.ToLongDict();

                    // if refund receive, attach a refund reason.
                    if (context.State == WFState.NormalReceive || context.State == WFState.RefundReceive)
                    {
                        b.AddTag("auth", context.AuthResult.Result.ToString());
                    }
                });
            return votblk;
        }
    }
}
