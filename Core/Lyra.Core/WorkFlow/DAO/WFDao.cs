using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.DAO
{
    [LyraWorkFlow]
    public class WFDao : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DAO_CRDAO,
                RecvVia = BrokerRecvType.PFRecv,
                Steps = new[] { GenesisAsync }
            };
        }

        public async Task<TransactionBlock> GenesisAsync(DagSystem sys, SendTransferBlock send)
        {
            var name = send.Tags["name"];
            var desc = send.Tags["desc"];
            var sellerPar = int.Parse(send.Tags["sellerPar"]);
            var buyerPar = int.Parse(send.Tags["buyerPar"]);

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{name},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var exists = await sys.Storage.FindFirstBlockAsync(AccountId);
            if (exists != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var daogen = new DaoGenesisBlock
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

                // profiting
                PType = ProfitingType.Orgnization,

                // dao
                Description = desc,
                SellerPar = sellerPar,
                BuyerPar = buyerPar,
                Treasure = new Dictionary<string, long>(),
            };

            daogen.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            daogen.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return daogen;
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            decimal shareRito;
            int seats;

            if (
                send.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(send.Tags["name"]) &&
                send.Tags.ContainsKey("desc") && !string.IsNullOrWhiteSpace(send.Tags["desc"]) &&
                send.Tags.ContainsKey("sellerPar") && int.TryParse(send.Tags["sellerPar"], out int sellerPar) &&
                send.Tags.ContainsKey("buyerPar") && int.TryParse(send.Tags["sellerPar"], out int buyerPar)
                        && send.Tags.ContainsKey("share") && decimal.TryParse(send.Tags["share"], out shareRito)
                        && send.Tags.ContainsKey("seats") && int.TryParse(send.Tags["seats"], out seats)

                && send.Tags.Count == 7
                )
            {
                var name = send.Tags["name"];
                var desc = send.Tags["desc"];

                // profiting
                if (shareRito >= 0m && shareRito <= 1m && seats >= 0 && seats <= 100)
                {

                }
                else
                    return APIResultCodes.InvalidShareRitio;

                if (shareRito == 0 && seats != 0)
                    return APIResultCodes.InvalidShareRitio;

                if (shareRito > 0 && seats == 0)
                    return APIResultCodes.InvalidShareRitio;

                if (shareRito > 1)
                    return APIResultCodes.InvalidShareRitio;

                if (name.Length < 3)
                    return APIResultCodes.InputTooShort;

                if (name.Length > 100 || desc.Length > 300)
                    return APIResultCodes.InputTooLong;

                if (send.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                    return APIResultCodes.InvalidServiceRequest;

                if(sellerPar < 0 || sellerPar > 1000 || buyerPar < 0 || buyerPar > 1000)
                    return APIResultCodes.InvalidTagParameters;

                // check name dup
                var existsdao = sys.Storage.GetDaoByName(name);
                if (existsdao != null)
                    return APIResultCodes.DuplicateName;

                return APIResultCodes.Success;
            }
            else
                return APIResultCodes.InvalidTagParameters;
        }
    }
}
