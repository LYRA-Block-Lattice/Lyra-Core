using Lyra.Core.Blocks;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class OTCOrderRecvAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCOrderRecv;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OtcOrderRecvBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OtcOrderRecvBlock;

            if (block.Order.fiatPrice.CountDecimalDigits() > 8 || 
                block.Order.amount.CountDecimalDigits() > 8 || 
                block.Order.limitMin.CountDecimalDigits() > 8 ||
                block.Order.limitMax.CountDecimalDigits() > 8 ||
                block.Order.price.CountDecimalDigits() > 8 ||
                block.Order.collateral.CountDecimalDigits() > 8 ||
                block.Order.collateralPrice.CountDecimalDigits() > 8
                )
                return APIResultCodes.InvalidDecimalDigitalCount;

            // related tx must exist 
            //var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            //if (relTx == null || relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            //{
            //    // verify its pf or dao
            //    var daog = await sys.Storage.FindFirstBlockAsync(relTx.DestinationAccountId) as DaoGenesisBlock;
            //    if(daog == null && relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            //        return APIResultCodes.InvalidServiceRequest;
            //}

            //// service must not been processed
            //var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            //if (processed.Count != 0)
            //    return APIResultCodes.InvalidServiceRequest;

            //var name = relTx.Tags["name"];

            //// create a semi random account for pool.
            //// it can be verified by other nodes.
            //var keyStr = $"{relTx.Hash.Substring(0, 16)},{name},{relTx.AccountID}";
            //var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            //if (block.AccountID != AccountId)
            //    return APIResultCodes.InvalidAccountId;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DaoAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class OTCOrderSendAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCOrderSend;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OtcOrderSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OtcOrderSendBlock;

            if (block.Order.fiatPrice.CountDecimalDigits() > 8 ||
                block.Order.amount.CountDecimalDigits() > 8 ||
                block.Order.limitMin.CountDecimalDigits() > 8 ||
                block.Order.limitMax.CountDecimalDigits() > 8 ||
                block.Order.price.CountDecimalDigits() > 8 ||
                block.Order.collateral.CountDecimalDigits() > 8 ||
                block.Order.collateralPrice.CountDecimalDigits() > 8
                )
                return APIResultCodes.InvalidDecimalDigitalCount;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (relTx == null || relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            {
                // verify its pf or dao
                var daog = await sys.Storage.FindFirstBlockAsync(relTx.DestinationAccountId) as DaoGenesisBlock;
                if (daog == null && relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                    return APIResultCodes.InvalidServiceRequest;
            }

            // service must not been processed
            var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            if (processed.Count != 1)
                return APIResultCodes.InvalidServiceRequest;

            //var name = relTx.Tags["name"];

            //// create a semi random account for pool.
            //// it can be verified by other nodes.
            //var keyStr = $"{relTx.Hash.Substring(0, 16)},{name},{relTx.AccountID}";
            //var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            //if (block.AccountID != AccountId)
            //    return APIResultCodes.InvalidAccountId;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DaoAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class OTCOrderGenesisAuthorizer : OTCOrderRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCOrderGenesis;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OTCOrderGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OTCOrderGenesisBlock;

            if(block.AccountType != AccountTypes.OTC)
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DaoGenesisAuthorizer->DaoAuthorizer");
        }
    }
}
