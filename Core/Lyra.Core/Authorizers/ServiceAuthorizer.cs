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

                // authorizers
                if (block.Authorizers.Count > LyraGlobal.MAXIMUM_AUTHORIZERS
                    || block.Authorizers.Count < LyraGlobal.MINIMUM_AUTHORIZERS)
                    //|| block.Authorizers.Count < (prevBlock as ServiceBlock).Authorizers.Count)
                    return APIResultCodes.InvalidAuthorizerCount;

                // authorizer count should be at least 90% of all voters.
                var validAuthorizersList = GetValidVoters(board, prevBlock);
                if (block.Authorizers.Count < 0.9 * validAuthorizersList.Count())
                    return APIResultCodes.InvalidAuthorizerCount;
            }
            else
            {
                if (block.Authorizers.Count < LyraGlobal.MINIMUM_AUTHORIZERS)
                    return APIResultCodes.InvalidAuthorizerCount;
            }

            foreach (var kvp in block.Authorizers)
            {
                var signAgainst = prevBlock?.Hash ?? ProtocolSettings.Default.StandbyValidators[0];
                if (!Signatures.VerifyAccountSignature(signAgainst, kvp.Key, kvp.Value))
                {
                    return APIResultCodes.InvalidAuthorizerSignatureInServiceBlock;
                }

                // verify vote etc.
                if (!board.AllVoters.Contains(kvp.Key))
                    return APIResultCodes.InvalidAuthorizerInServiceBlock;
            }

            // check CreateNewViewAsNewLeaderAsync in Consensus.cs
            List<string> GetValidVoters(BillBoard board, Block prevSvcBlock)
            {
                var list = new List<string>();
                foreach (var voter in board.AllVoters)
                {
                    if (board.ActiveNodes.Any(a => a.AccountID == voter))
                    {
                        var node = board.ActiveNodes.First(a => a.AccountID == voter);

                        if (Signatures.VerifyAccountSignature(prevSvcBlock.Hash, node.AccountID, node.AuthorizerSignature))
                        {
                            list.Add(node.AccountID);
                        }
                    }
                    else
                    {
                        // impossible. viewchangehandler has already filterd all none active messages.
                        // or just bypass it?
                    }

                    if (list.Count >= LyraGlobal.MAXIMUM_AUTHORIZERS)
                        break;
                }
                return list;
            }

            //if(block.Height > 1)        // no genesis block
            //{
            //    var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
            //    var allVoters = sys.Storage.FindVotes(board.AllVoters).OrderByDescending(a => a.Amount);

            //    foreach (var authorizer in block.Authorizers) // they can be listed in different order!
            //    {
            //        if (!allVoters.Any(a => a.AccountId == authorizer.AccountID) ||
            //            allVoters.First(a => a.AccountId == authorizer.AccountID).Amount < LyraGlobal.MinimalAuthorizerBalance ||
            //            !Signatures.VerifyAccountSignature(authorizer.IPAddress, authorizer.AccountID, authorizer.Signature))
            //            return APIResultCodes.InvalidAuthorizerInBillBoard;
            //    }
            //}

            return APIResultCodes.Success;
        }
    }
}
