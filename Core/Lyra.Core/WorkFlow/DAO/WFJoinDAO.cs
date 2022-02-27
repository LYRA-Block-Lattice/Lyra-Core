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

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            long amount = 0;
            if (send.Tags.Count != 3 ||
                !send.Tags.ContainsKey("daoid") ||
                string.IsNullOrWhiteSpace(send.Tags["daoid"]) ||
                !send.Tags.ContainsKey("amount") ||
                !long.TryParse(send.Tags["amount"], out amount)
                )
                return APIResultCodes.InvalidBlockTags;

            // dao must exists
            var dao = sys.Storage.FindLatestBlockAsync(send.Tags["daoid"]);
            if (dao == null)
                return APIResultCodes.InvalidDAO;

            // min amount to invest
            if (amount.ToBalanceDecimal() < 10000)
                return APIResultCodes.InvalidAmount;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> MainAsync(DagSystem sys, SendTransferBlock send)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            if (recv != null)
                return null;

            var daoid = send.Tags["daoid"];
            var index = long.Parse(send.Tags["amount"]);

            var prevBlock = await sys.Storage.FindLatestBlockAsync(daoid) as TransactionBlock;
            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);
            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            return await TransactionOperateAsync(sys, send, 
                () => prevBlock.GenInc<DaoRecvBlock>(),
                (b) =>
                {
                    // treasure change
                    var depositBalance = new Dictionary<string, decimal>();
                    var depositShares = new Dictionary<string, decimal>();
                    if (b.Balances.Any())
                    {
                        var lastBalance = prevBlock.Balances.ToDecimalDict();
                        var lastShares = ((IDao)prevBlock).Treasure.ToRitoDecimalDict();

                        // the rito must be preserved for every deposition
                        //var poolRito = lastBalance[LyraGlobal.OFFICIALTICKERCODE] / lastBalance[poolGenesis.Token1];
                        foreach (var oldBalance in lastBalance)
                        {
                            depositBalance.Add(oldBalance.Key, oldBalance.Value + txInfo.Changes[oldBalance.Key]);
                        }

                        var prevBalance = lastBalance[LyraGlobal.OFFICIALTICKERCODE];
                        var curBalance = depositBalance[LyraGlobal.OFFICIALTICKERCODE];

                        foreach (var share in lastShares)
                        {
                            depositShares.Add(share.Key, (share.Value * prevBalance / curBalance));
                        }

                        // merge share if any
                        var r0 = txInfo.Changes[LyraGlobal.OFFICIALTICKERCODE] / curBalance;

                        if (depositShares.ContainsKey(send.AccountID))
                            depositShares[send.AccountID] += r0;
                        else
                            depositShares.Add(send.AccountID, r0);
                    }
                    else
                    {
                        foreach (var token in txInfo.Changes)
                        {
                            depositBalance.Add(token.Key, token.Value);
                        }

                        depositShares.Add(send.AccountID, 1m);   // 100%
                    }

                    b.Balances = depositBalance.ToLongDict();
                    (b as IDao).Treasure = depositShares.ToRitoLongDict();
                });
        }

    }
}
