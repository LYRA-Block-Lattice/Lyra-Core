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

namespace Lyra.Core.WorkFlow.DLR
{
    [LyraWorkFlow]
    public class WFCreateDealer : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DLR_CREATE,
                RecvVia = BrokerRecvType.GuildRecv,
                Steps = new[] { DealerGenesisAsync }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            if (
                send.Tags.ContainsKey("objType") && send.Tags["objType"] == nameof(DealerCreateArgument) &&
                send.Tags.ContainsKey("data") && !string.IsNullOrWhiteSpace(send.Tags["data"]) &&                
                send.Tags.Count == 3
                )
            {
                DealerCreateArgument arg;
                try
                {
                    arg = JsonConvert.DeserializeObject<DealerCreateArgument>(send.Tags["data"]);
                    if (arg == null)
                        throw new Exception();
                }
                catch
                {
                    return APIResultCodes.InvalidArgument;
                }

                // validate the argument
                if (!Signatures.ValidateAccountId(arg.DealerAccountId))
                    return APIResultCodes.InvalidAccountId;

                if (arg.Mode != ClientMode.Permissionless)
                    return APIResultCodes.InvalidArgument;

                // check name dup
                var existsdealer = sys.Storage.GetDealerByName(arg.Name);
                if (existsdealer != null)
                    return APIResultCodes.DuplicateName;

                return APIResultCodes.Success;
            }
            else
                return APIResultCodes.InvalidTagParameters;
        }

        public async Task<TransactionBlock?> DealerGenesisAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var arg = JsonConvert.DeserializeObject<DealerCreateArgument>(send.Tags["data"]);

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{arg.DealerAccountId},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var exists = await sys.Storage.FindFirstBlockAsync(AccountId);
            if (exists != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var daogen = new DealerGenesisBlock
            {
                Height = 1,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = AccountTypes.Server,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),

                // broker
                Name = arg.Name,
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // dealer
                Endpoint = arg.ServiceUrl,
                Description = arg.Description,
            };

            daogen.AddTag(Block.MANAGEDTAG, context.State.ToString());

            // pool blocks are service block so all service block signed by leader node
            daogen.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return daogen;
        }
    }
}
