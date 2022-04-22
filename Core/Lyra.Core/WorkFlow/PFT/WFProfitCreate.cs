using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.PFT
{
    [LyraWorkFlow]//v
    public class WFProfitCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_PFT_CRPFT,
                RecvVia = BrokerRecvType.PFRecv,
            };
        }

        #region BRK_PFT_CRPFT
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock lastBlock)
        {
            var chgs = block.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            // create profiting
            if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.ProfitingAccountCreateFee)
                return APIResultCodes.InvalidFeeAmount;

            ProfitingType ptype;
            decimal shareRito;
            int seats;
            if (
                block.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(block.Tags["name"]) &&
                block.Tags.ContainsKey("ptype") && Enum.TryParse(block.Tags["ptype"], false, out ptype)
                && block.Tags.ContainsKey("share") && decimal.TryParse(block.Tags["share"], out shareRito)
                && block.Tags.ContainsKey("seats") && int.TryParse(block.Tags["seats"], out seats)
                && block.Tags.Count == 5
                )
            {
                if (shareRito >= 0m && shareRito <= 1m && seats >= 0 && seats <= 100)
                {
                    // name dup check
                    var pfts = await sys.Storage.FindAllProfitingAccountForOwnerAsync(block.AccountID);
                    if (pfts.Any(a => a.Name == block.Tags["name"]))
                        return APIResultCodes.DuplicateName;

                    // one type per account. just keep it simple.
                    if (pfts.Any(a => a.PType == ptype))
                        return APIResultCodes.DuplicateAccountType;

                    if (shareRito == 0 && seats != 0)
                        return APIResultCodes.InvalidShareRitio;

                    if (shareRito > 0 && seats == 0)
                        return APIResultCodes.InvalidShareRitio;

                    var dupname = sys.Storage.FindProfitingAccountsByName(block.Tags["name"]);
                    if (dupname != null)
                        return APIResultCodes.DuplicateName;
                }
                else
                {
                    return APIResultCodes.InvalidShareOfProfit;
                }
            }
            else
                return APIResultCodes.InvalidBlockTags;

            return APIResultCodes.Success;
        }
        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var pgen = blocks.FirstOrDefault(a => a is ProfitingGenesis);
            if (pgen != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();

            // create a semi random account for pool.
            // it can be verified by other nodes.
            decimal shareRito = decimal.Parse(send.Tags["share"]);
            var keyStr = $"{send.Hash.Substring(0, 16)},{send.Tags["ptype"]},{shareRito.ToBalanceLong()},{send.Tags["seats"]},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            ProfitingType ptype;
            Enum.TryParse(send.Tags["ptype"], out ptype);
            var pftGenesis = new ProfitingGenesis
            {
                Height = 1,
                Name = send.Tags["name"],
                OwnerAccountId = send.AccountID,
                AccountType = AccountTypes.Profiting,
                AccountID = AccountId,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // pool specified config
                PType = ptype,
                ShareRito = decimal.Parse(send.Tags["share"]),
                Seats = int.Parse(send.Tags["seats"]),
                RelatedTx = send.Hash
            };

            pftGenesis.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            pftGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return pftGenesis;
        }

        #endregion
    }

}
