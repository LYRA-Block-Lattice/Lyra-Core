using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.ABI;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Data.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.FAT
{
    [LyraWorkFlow]
    public class WFPrintFiatMoney : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_FIAT_PRINT,
                RecvVia = BrokerRecvType.GuildRecv,
                Steps = new[] {
                    MintAsync,
                    SendToOwnerAsync
                }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (
                send.Tags.ContainsKey("objType") && send.Tags["objType"] == nameof(FiatPrintMoney) &&
                send.Tags.ContainsKey("data") && !string.IsNullOrWhiteSpace(send.Tags["data"]) &&
                send.Tags.Count == 3
                )
            {
                FiatPrintMoney arg;
                try
                {
                    arg = JsonConvert.DeserializeObject<FiatPrintMoney>(send.Tags["data"]);
                    if (arg == null)
                        throw new Exception();
                }
                catch
                {
                    return APIResultCodes.InvalidTagParameters;
                }

                // validate the argument
                //var ticker = "fiat/" + arg.symbol;
                //var gens = await sys.Storage.FindTokenGenesisBlockAsync(null, ticker);
                //if (gens == null)
                //    return APIResultCodes.InvalidTagParameters;

                // make sure the wallet exists
                var existsWallet = await sys.Storage.FindFiatWalletAsync(send.AccountID, arg.symbol);
                if (existsWallet == null)
                    return APIResultCodes.AccountDoesNotExist;

                return APIResultCodes.Success;
            }
            else
                return APIResultCodes.InvalidTagParameters;
        }
        protected async Task<TransactionBlock?> MintAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is FiatPrintBlock))
                return null;

            var arg = JsonConvert.DeserializeObject<FiatPrintMoney>(send.Tags["data"]);

            var ticker = arg.symbol;
            var last = await sys.Storage.FindFiatWalletAsync(send.AccountID, arg.symbol);
            var lastfiat = last as IFiatWallet;
            var gensis = await sys.Storage.FindTokenGenesisBlockAsync(null, ticker);

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var mint = new FiatPrintBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountID = last.AccountID,        // in fact we not use this account.
                Balances = new SortedDictionary<string, long>(),

                // broker
                Name = lastfiat.Name,
                OwnerAccountId = lastfiat.OwnerAccountId,
                RelatedTx = send.Hash,

                // Dex wallet
                ExtSymbol = lastfiat.ExtSymbol,

                // mint
                GenesisHash = gensis.Hash,
                MintAmount = arg.amount.ToBalanceLong()
            };

            mint.Balances = last.Balances.ToDecimalDict().ToLongDict();
            if (mint.Balances.ContainsKey(ticker))
                mint.Balances[ticker] += arg.amount.ToBalanceLong();
            else
                mint.Balances.Add(ticker, arg.amount.ToBalanceLong());

            mint.AddTag(Block.MANAGEDTAG, context.State.ToString());

            mint.InitializeBlock(last, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return mint;
        }

        protected async Task<TransactionBlock?> SendToOwnerAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            var arg = JsonConvert.DeserializeObject<FiatPrintMoney>(send.Tags["data"]);
            var prevBlock = await sys.Storage.FindFiatWalletAsync(send.AccountID, arg.symbol);
            var votblk = await TransactionOperateAsync(sys, send.Hash, prevBlock,
                () => prevBlock.GenInc<FiatSendBlock>(),
                () => context.State,
                (b) =>
                {
                    // recv
                    b.DestinationAccountId = send.AccountID;

                    // broker
                    (b as IBrokerAccount).RelatedTx = send.Hash;

                    // send fiat
                    var oldbalance = prevBlock.Balances.ToDecimalDict();
                    oldbalance[arg.symbol] = 0;                    
                    b.Balances = oldbalance.ToLongDict();
                });
            return votblk;
        }
    }
}
