using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lyra.Core.Decentralize.ConsensusService;

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

            // verify fees
            if (prevBlock != null)
            {
                var allConsBlocks = await sys.Storage.GetConsolidationBlocksAsync(prevBlock.Hash);
                var feesGened = allConsBlocks.Sum(a => a.totalFees);

                if (block.FeesGenerated != feesGened)
                    return APIResultCodes.InvalidServiceBlockTotalFees;
            }

            // authorizers
            if (block.Authorizers.Count > LyraGlobal.MAXMIMUMAUTHORIZERS
                || block.Authorizers.Count < (prevBlock as ServiceBlock).Authorizers.Count)
                return APIResultCodes.InvalidAuthorizerCount;

            var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
            for(int i = 0; i < block.Authorizers.Count; i++)
            {
                if (!block.Authorizers[i].AccountID.Equals(board.PrimaryAuthorizers[i]))
                    return APIResultCodes.InvalidAuthorizerInBillBoard;
            }

            return APIResultCodes.Success;
        }
    }
}
