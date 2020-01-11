using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Authorizers
{
    public class ServiceAuthorizer : BaseAuthorizer
    {
        public override (APIResultCodes, AuthorizationSignature) Authorize<T>(T tblock, bool WithSign = true)
        {
            var result = AuthorizeImpl(tblock);
            if (APIResultCodes.Success == result && WithSign)
            {
                return (APIResultCodes.Success, Sign(tblock));
            }                

            return (result, (AuthorizationSignature)null);
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            return APIResultCodes.Success;
        }

        private APIResultCodes AuthorizeImpl<T>(T tblock)
        {
            if (!(tblock is ServiceBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ServiceBlock;

            // 1. check if the block already exists
            if (null != BlockChain.Singleton.GetBlockByUIndex(block.UIndex))
                return APIResultCodes.BlockWithThisIndexAlreadyExists;

            // service specifice feature
            //block.

            var result = VerifyBlock(block, null);
            if (result != APIResultCodes.Success)
                return result;

            return APIResultCodes.Success;
        }
    }
}
