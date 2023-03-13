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
    public class WFLeaveDAO : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DAO_LEAVE,
                RecvVia = BrokerRecvType.DaoRecv,
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

            // invest must exists
            if (!(dao as IDao).Treasure.ContainsKey(send.AccountID))
                return APIResultCodes.AccountDoesNotExist;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> MainAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            // check exists
            var daoid = send.Tags["daoid"];

            var prevBlock = await sys.Storage.FindLatestBlockAsync(daoid) as TransactionBlock;
            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            return await TransactionOperateAsync(sys, send.Hash, prevBlock, 
                () => prevBlock.GenInc<DaoSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // send
                    (b as SendTransferBlock).DestinationAccountId = send.AccountID;

                    // treasure change
                    var curBalance = b.Balances.ToDecimalDict();
                    var curShares = (b as IDao).Treasure.ToDecimalDict();

                    curBalance["LYR"] -= curShares[send.AccountID];
                    curShares.Remove(send.AccountID);

                    b.Balances = curBalance.ToLongDict();
                    (b as IDao).Treasure = curShares.ToLongDict();
                });
        }

    }
}
