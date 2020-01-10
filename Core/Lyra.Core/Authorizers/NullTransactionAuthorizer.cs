using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Authorizers
{
    public class NullTransactionAuthorizer : BaseAuthorizer
    {
        public override (APIResultCodes, AuthorizationSignature) Authorize<T>(T tblock)
        {
            var result = AuthorizeImpl(tblock);
            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(tblock));
            else
                return (result, (AuthorizationSignature)null);
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            return APIResultCodes.Success;
        }

        private APIResultCodes AuthorizeImpl<T>(T tblock)
        {
            if (!(tblock is NullTransactionBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as NullTransactionBlock;

            // 1. check if the block already exists
            if (null != BlockChain.Singleton.GetBlockByUIndex(block.UIndex))
                return APIResultCodes.BlockWithThisIndexAlreadyExists;

            var lastCons = BlockChain.Singleton.GetSyncBlock();
            if (lastCons == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            if (block.PreviousConsolidateHash != lastCons.Hash)
                return APIResultCodes.PreviousBlockNotFound;

            var result = VerifyBlock(block, null);
            if (result != APIResultCodes.Success)
                return result;

            return APIResultCodes.Success;
        }
    }
}
