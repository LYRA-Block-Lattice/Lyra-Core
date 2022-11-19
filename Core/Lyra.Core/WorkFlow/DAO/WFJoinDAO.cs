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
    public class WFJoinDAO : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DAO_JOIN,
                RecvVia = BrokerRecvType.None,
                Steps = new[] { MainAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("daoid") ||
                string.IsNullOrWhiteSpace(send.Tags["daoid"])
                )
                return APIResultCodes.InvalidBlockTags;

            // dao must exists
            var dao = await sys.Storage.FindLatestBlockAsync(send.Tags["daoid"]);
            if (dao == null)
                return APIResultCodes.InvalidDAO;

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);
            if (txInfo.Changes.Count != 1 || !txInfo.Changes.ContainsKey("LYR"))
                return APIResultCodes.InvalidToken;

            //min amount to invest
            var amount = txInfo.Changes["LYR"];
            if (amount < 10000)
                return APIResultCodes.InvalidAmount;

            return APIResultCodes.Success;
        }

        public override async Task<ReceiveTransferBlock?> NormalReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await MainAsync(sys, context) as ReceiveTransferBlock;
        }

        public override async Task<ReceiveTransferBlock> RefundReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await MainAsync(sys, context) as ReceiveTransferBlock;
        }

        async Task<TransactionBlock> MainAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            if (recv != null)
                return null;

            var daoid = send.Tags["daoid"];

            var prevBlock = await sys.Storage.FindLatestBlockAsync(daoid) as TransactionBlock;
            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            return await TransactionOperateAsync(sys, send.Hash, prevBlock, 
                () => prevBlock.GenInc<DaoRecvBlock>(),
                () => WFState.Finished,
                (b) =>
                {
                    // recv
                    (b as ReceiveTransferBlock).SourceHash = send.Hash;
                    
                    // balance record the real funds.
                    // treasure record who invest what amount of LYR.

                    // treasure change
                    var lastBalance = prevBlock.Balances.ToDecimalDict();
                    var lastShares = ((IDao)prevBlock).Treasure.ToDecimalDict();

                    if(lastBalance.ContainsKey("LYR"))
                    {
                        lastBalance["LYR"] += txInfo.Changes["LYR"];
                    }
                    else
                    {
                        lastBalance["LYR"] = txInfo.Changes["LYR"];
                    }

                    if (lastShares.ContainsKey(send.AccountID))
                        lastShares[send.AccountID] += txInfo.Changes["LYR"];
                    else
                        lastShares.Add(send.AccountID, txInfo.Changes["LYR"]);


                    b.Balances = lastBalance.ToLongDict();
                    (b as IDao).Treasure = lastShares.ToLongDict();
                });
        }

    }
}
