using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class BrokerAccountAuthorizer : TransactionAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var block = tblock as IBrokerAccount;
            if (block == null)
                return APIResultCodes.InvalidBlockType;

            if (string.IsNullOrWhiteSpace(block.Name))
                return APIResultCodes.InvalidName;

            if (!await sys.Storage.AccountExistsAsync(block.OwnerAccountId))
                return APIResultCodes.AccountDoesNotExist;

            var send = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (send == null)
                return APIResultCodes.InvalidRelatedTx;

            return APIResultCodes.Success;
        }
    }

    public class BrokerAccountRecvAuthorizer : ReceiveTransferAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var block = tblock as BrokerAccountRecv;
            if (block == null)
                return APIResultCodes.InvalidBlockType;

            // may have multiple receive. like profiting block.
            //var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            //if (blocks.Count != 0)
            //    return APIResultCodes.InvalidRelatedTx;

            // IBrokerAccount interface

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var brkauth = new BrokerAccountAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);

            stopwatch.Stop();
            Console.WriteLine($"AuthImpl BrokerAccountAuthorizer uses {stopwatch.ElapsedMilliseconds} ms");

            if (brkret.Item1 == APIResultCodes.Success)
                return await MeasureAuthAsync("BrokerAccountRecvAuthorizer", base.GetType().Name, base.AuthorizeImplAsync(sys, tblock));
            else
                return brkret.Item1;
        }
    }

    public class BrokerAccountSendAuthorizer : SendTransferAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            var block = tblock as BrokerAccountSend;
            if (block == null)
                return APIResultCodes.InvalidBlockType;

            // benefiting may have multiple blocks
            if(!(block is BenefitingBlock))
            {
                var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
                if (blocks.Count != 0)
                    return APIResultCodes.InvalidRelatedTx;
            }

            // IBrokerAccount interface
            var brkauth = new BrokerAccountAuthorizer();
            var brkret = await brkauth.AuthorizeAsync(sys, tblock);
            if (brkret.Item1 == APIResultCodes.Success)
                return await MeasureAuthAsync(this.GetType().Name, base.GetType().Name, base.AuthorizeImplAsync(sys, tblock));
            else
                return brkret.Item1;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.NoFee;
        }
    }
}
