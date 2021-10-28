using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Core.Decentralize;
using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lyra.Core.Decentralize.ConsensusService;
using Lyra.Data.API;

namespace Lyra.Core.Authorizers
{
    public class ServiceAuthorizer : BaseAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }

        protected override Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            return Task.FromResult(APIResultCodes.Success);
        }

        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ServiceBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ServiceBlock;

            var prevBlock = await sys.Storage.FindBlockByHashAsync(block.PreviousHash);

            var result = await VerifyBlockAsync(sys, block, prevBlock);
            if (result != APIResultCodes.Success)
                return result;

            // service specifice feature
            if (block.FeeTicker != LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.InvalidFeeTicker;

            var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());

            if (prevBlock != null) // only if this is not very first service block
            {
                // verify fees
                var allConsBlocks = await sys.Storage.GetConsolidationBlocksAsync(prevBlock.Hash);
                var feesGened = allConsBlocks.Sum(a => a.totalFees);

                if (block.FeesGenerated != feesGened)
                    return APIResultCodes.InvalidServiceBlockTotalFees;

                var signAgainst = prevBlock?.Hash ?? ProtocolSettings.Default.StandbyValidators[0];

                foreach (var voter in board.AllVoters)
                {
                    var node = board.ActiveNodes.FirstOrDefault(a => a.AccountID == voter);
                    if (node != null && Signatures.VerifyAccountSignature(signAgainst, node.AccountID, node.AuthorizerSignature))
                    {
                        if (!block.Authorizers.ContainsKey(voter))
                            return APIResultCodes.InvalidAuthorizerInServiceBlock;
                    }
                }

                if(block.Authorizers.Keys.Any(a => !board.AllVoters.Contains(a)))
                {
                    return APIResultCodes.InvalidAuthorizerInServiceBlock;
                }
            }
            else // svc gensis
            {
                if (block.Authorizers.Count < LyraGlobal.MINIMUM_AUTHORIZERS)
                    return APIResultCodes.InvalidAuthorizerCount;

                var signAgainst = ProtocolSettings.Default.StandbyValidators[0];
                foreach (var pn in ProtocolSettings.Default.StandbyValidators)
                {
                    if (!board.ActiveNodes.Any(a => a.AccountID == pn))
                        return APIResultCodes.InvalidAuthorizerInServiceBlock;

                    if (!Signatures.VerifyAccountSignature(signAgainst, pn, block.Authorizers[pn]))
                    {
                        return APIResultCodes.InvalidAuthorizerSignatureInServiceBlock;
                    }
                }
            }

            return APIResultCodes.Success;
        }
    }
}
