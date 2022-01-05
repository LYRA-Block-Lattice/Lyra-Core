using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize.WorkFlow
{
    public class WFDao : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DAO_CRDAO,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new []{
                    new BlockDesc
                    {
                        BlockType = BlockTypes.OrgnizationGenesis,
                        TheBlock = typeof(DaoGenesis),
                        AuthorizerName = "DaoGenesisAuthorizer",
                    }
                }
            };
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            var name = send.Tags["name"];

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{name},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var daogen = new DaoGenesis
            {
                Height = 1,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = AccountTypes.DAO,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),

                // broker
                Name = name,
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // dao
                Description = "hahaha",
                Treasure = new Dictionary<string, long>(),
            };

            daogen.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            daogen.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return daogen;
        }

        public override Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (
                send.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(send.Tags["name"])
                && send.Tags.Count == 2
                )
            {
                if (send.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                    return Task.FromResult(APIResultCodes.InvalidServiceRequest);

                // check name dup
                var name = send.Tags["name"];
                var existsdao = sys.Storage.GetDaoByName(name);
                if (existsdao != null)
                    return Task.FromResult(APIResultCodes.DuplicateName);

                return Task.FromResult(APIResultCodes.Success);
            }
            else
                return Task.FromResult(APIResultCodes.InvalidBlockTags);
        }
    }
}
