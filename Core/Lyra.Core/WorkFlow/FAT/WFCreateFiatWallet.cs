using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.ABI;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
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
    public class WFCreateFiatWallet : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_FIAT_CRACT,
                RecvVia = BrokerRecvType.GuildRecv,
                Steps = new[] { FiatGenesisAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (
                send.Tags.ContainsKey("objType") && send.Tags["objType"] == nameof(FiatCreateWallet) &&
                send.Tags.ContainsKey("data") && !string.IsNullOrWhiteSpace(send.Tags["data"]) &&                
                send.Tags.Count == 3
                )
            {
                FiatCreateWallet arg;
                try
                {
                    arg = JsonConvert.DeserializeObject<FiatCreateWallet>(send.Tags["data"]);
                    if (arg == null)
                        throw new Exception();
                }
                catch
                {
                    return APIResultCodes.InvalidTagParameters;
                }

                // validate the argument
                var ticker = arg.symbol;
                var gens = await sys.Storage.FindTokenGenesisBlockAsync(null, ticker);
                if (gens == null)
                    return APIResultCodes.InvalidTagParameters;

                // wallet should not exits
                var existsWallet = await sys.Storage.FindFiatWalletAsync(send.AccountID, ticker);
                if(existsWallet != null)
                    return APIResultCodes.AccountAlreadyExists;

                return APIResultCodes.Success;
            }
            else
                return APIResultCodes.InvalidTagParameters;
        }

        public async Task<TransactionBlock?> FiatGenesisAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var arg = JsonConvert.DeserializeObject<FiatCreateWallet>(send.Tags["data"]);

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{arg.symbol},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var exists = await sys.Storage.FindFirstBlockAsync(AccountId);
            if (exists != null)
                return null;

            var ticker = arg.symbol;
            var gens = await sys.Storage.FindTokenGenesisBlockAsync(null, ticker);
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var daogen = new FiatWalletGenesis
            {
                Height = 1,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = AccountTypes.Fiat,
                AccountID = AccountId,
                Balances = new SortedDictionary<string, long>(),

                // broker
                Name = $"Fiat wallet for {arg.symbol}",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                //
                ExtSymbol = arg.symbol,
                GenesisHash = gens.Hash,
            };

            daogen.AddTag(Block.MANAGEDTAG, context.State.ToString());

            // pool blocks are service block so all service block signed by leader node
            daogen.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return daogen;
        }
    }
}
