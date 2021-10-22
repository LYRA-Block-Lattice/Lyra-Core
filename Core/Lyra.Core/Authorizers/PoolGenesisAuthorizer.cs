using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class PoolGenesisAuthorizer : BaseAuthorizer
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
            if (!(tblock is PoolGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolGenesisBlock;

            // Validate blocks
            var result = await VerifyBlockAsync(sys, block, null);
            if (result != APIResultCodes.Success)
                return result;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx);
            if (relTx == null)
                return APIResultCodes.InvalidServiceRequest;

            // send account must be current owner
            var send = await sys.Storage.FindBlockByHashAsync((relTx as ReceiveTransferBlock).SourceHash) as SendTransferBlock;

            // service must not been processed
            var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            if (processed.Count != 0)
                return APIResultCodes.InvalidServiceRequest;

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{block.Token0},{block.Token1},{send.AccountID}";
            var randAccount = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            if (block.AccountID != randAccount.AccountId)
                return APIResultCodes.InvalidAccountId;

            return APIResultCodes.Success;
        }
    }
}
