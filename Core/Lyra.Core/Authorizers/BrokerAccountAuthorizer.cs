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
            var recv = tblock as BrokerAccountRecv;
            if (recv == null)
                return APIResultCodes.InvalidBlockType;

            if (string.IsNullOrWhiteSpace(recv.Name))
                return APIResultCodes.InvalidName;

            if (!await sys.Storage.AccountExistsAsync(recv.OwnerAccountId))
                return APIResultCodes.AccountDoesNotExist;

            if (null == await sys.Storage.FindBlockByHashAsync(recv.RelatedTx))
                return APIResultCodes.InvalidRelatedTx;

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
            var send = tblock as BrokerAccountSend;
            if (send == null)
                return APIResultCodes.InvalidBlockType;

            if (string.IsNullOrWhiteSpace(send.Name))
                return APIResultCodes.InvalidName;

            if (!await sys.Storage.AccountExistsAsync(send.OwnerAccountId))
                return APIResultCodes.AccountDoesNotExist;

            if (null == await sys.Storage.FindBlockByHashAsync(send.RelatedTx))
                return APIResultCodes.InvalidRelatedTx;

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
