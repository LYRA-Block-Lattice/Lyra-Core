using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class BrokerAccountRecvAuthorizer : ReceiveTransferAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var br = await base.AuthorizeAsync(sys, tblock);
            APIResultCodes result;
            if (br.Item1 == APIResultCodes.Success)
                result = await AuthorizeImplAsync(sys, tblock);
            else
                result = br.Item1;

            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var block = tblock as BrokerAccountRecv;
            if (block == null)
                return APIResultCodes.InvalidBlockType;

            if (string.IsNullOrWhiteSpace(block.Name))
                return APIResultCodes.InvalidName;

            if (!await sys.Storage.AccountExistsAsync(block.OwnerAccountId))
                return APIResultCodes.AccountDoesNotExist;

            var send = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (send == null)
                return APIResultCodes.InvalidRelatedTx;

            // may have multiple receive. like profiting block.
            //var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            //if (blocks.Count != 0)
            //    return APIResultCodes.InvalidRelatedTx;

            return APIResultCodes.Success;
        }
    }

    public class BrokerAccountSendAuthorizer : SendTransferAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var br = await base.AuthorizeAsync(sys, tblock);
            APIResultCodes result;
            if (br.Item1 == APIResultCodes.Success)
                result = await AuthorizeImplAsync(sys, tblock);
            else
                result = br.Item1;

            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var block = tblock as BrokerAccountSend;
            if (block == null)
                return APIResultCodes.InvalidBlockType;

            if (string.IsNullOrWhiteSpace(block.Name))
                return APIResultCodes.InvalidName;

            if (!await sys.Storage.AccountExistsAsync(block.OwnerAccountId))
                return APIResultCodes.AccountDoesNotExist;

            var send = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (send == null)
                return APIResultCodes.InvalidRelatedTx;

            // benefiting may have multiple blocks
            if(!(block is BenefitingBlock))
            {
                var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
                if (blocks.Count != 0)
                    return APIResultCodes.InvalidRelatedTx;
            }

            return APIResultCodes.Success;
        }

        protected override async Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            APIResultCodes result = APIResultCodes.Success;
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                result = APIResultCodes.InvalidFeeAmount;

            return result;
        }
    }
}
