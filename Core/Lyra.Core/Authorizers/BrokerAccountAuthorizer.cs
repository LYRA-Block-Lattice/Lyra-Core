using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class BrokerAccountAuthorizer
    {
        public virtual async Task<APIResultCodes> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var block = tblock as IBrokerAccount;
            if (block == null)
                return APIResultCodes.InvalidBlockType;

            if (string.IsNullOrWhiteSpace(block.Name))
                return APIResultCodes.InvalidName;

            if (!string.IsNullOrEmpty(block.OwnerAccountId) && !await sys.Storage.AccountExistsAsync(block.OwnerAccountId))
                return APIResultCodes.AccountDoesNotExist;

            var send = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (send == null)
                return APIResultCodes.InvalidRelatedTx;

            return APIResultCodes.Success;
        }
    }

    public abstract class BrokerAccountRecvAuthorizer : ReceiveTransferAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var block = tblock as BrokerAccountRecv;
            if (block == null)
                return APIResultCodes.InvalidBlockType;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (relTx == null)
            {
                return APIResultCodes.InvalidServiceRequest;
            }

            // may have multiple receive. like profiting block.
            //var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            //if (blocks.Count != 0)
            //    return APIResultCodes.InvalidRelatedTx;

            // IBrokerAccount interface

            var brkauth = new BrokerAccountAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);

            if (brkret == APIResultCodes.Success)
                return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "BrokerAccountRecvAuthorizer->ReceiveTransferAuthorizer");
            else
                return brkret;
        }
    }

    public abstract class BrokerAccountSendAuthorizer : SendTransferAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var block = tblock as BrokerAccountSend;
            if (block == null)
                return APIResultCodes.InvalidBlockType;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (relTx == null)
            {
                return APIResultCodes.InvalidServiceRequest;
            }

            // benefiting may have multiple blocks
            //if(!(block is BenefitingBlock))
            //{
            //    var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            //    if (blocks.Count != 0)
            //        return APIResultCodes.InvalidRelatedTx;
            //}

            // IBrokerAccount interface
            var brkauth = new BrokerAccountAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret == APIResultCodes.Success)
                return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "BrokerAccountSendAuthorizer->SendTransferAuthorizer");
            else
                return brkret;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.NoFee;
        }
    }
}
