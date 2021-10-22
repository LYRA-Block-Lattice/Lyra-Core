using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class ProfitingAuthorizer : BaseAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);

            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ProfitingBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ProfitingBlock;

            // Validate blocks
            var result = await VerifyBlockAsync(sys, block, null);
            if (result != APIResultCodes.Success)
                return result;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (relTx == null)
                return APIResultCodes.InvalidServiceRequest;

            if (relTx.AccountID != block.OwnerAccountId)
                return APIResultCodes.InvalidServiceRequest;

            // service must not been processed
            var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            if (processed.Count != 0)
                return APIResultCodes.InvalidServiceRequest;

            // first verify account id
            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{relTx.Hash.Substring(0, 16)},{block.PType},{block.ShareRito},{block.Seats},{relTx.AccountID}";
            var (_, AccountId) = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            if (block.AccountID != AccountId)
                return APIResultCodes.InvalidAccountId;

            return APIResultCodes.Success;
        }
    }
}
