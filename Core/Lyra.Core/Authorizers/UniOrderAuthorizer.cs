using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
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
    public class UniOrderRecvAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderRecv;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UniOrderRecvBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UniOrderRecvBlock;

            if (
                block.Order.amount.CountDecimalDigits() > 8 || 
                block.Order.limitMin.CountDecimalDigits() > 8 ||
                block.Order.limitMax.CountDecimalDigits() > 8 ||
                block.Order.price.CountDecimalDigits() > 8 ||
                block.Order.cltamt.CountDecimalDigits() > 8
                )
                return APIResultCodes.InvalidDecimalDigitalCount;

            if (
                string.IsNullOrWhiteSpace(block.Order.dealerId))
                return APIResultCodes.InvalidOrder;

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

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "UniOrderRecvAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class UniOrderSendAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderSend;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UniOrderSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UniOrderSendBlock;

            if (
                block.Order.amount.CountDecimalDigits() > 8 ||
                block.Order.limitMin.CountDecimalDigits() > 8 ||
                block.Order.limitMax.CountDecimalDigits() > 8 ||
                block.Order.price.CountDecimalDigits() > 8 ||
                block.Order.cltamt.CountDecimalDigits() > 8
                )
                return APIResultCodes.InvalidDecimalDigitalCount;

            if (
                string.IsNullOrWhiteSpace(block.Order.dealerId))
                return APIResultCodes.InvalidOrder;

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
            //var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            //if (processed.Count < 1)
            //    return APIResultCodes.InvalidServiceRequest;

            //var name = relTx.Tags["name"];

            //// create a semi random account for pool.
            //// it can be verified by other nodes.
            //var keyStr = $"{relTx.Hash.Substring(0, 16)},{name},{relTx.AccountID}";
            //var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            //if (block.AccountID != AccountId)
            //    return APIResultCodes.InvalidAccountId;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "UniOrderSendAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class UniOrderGenesisAuthorizer : UniOrderRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UniOrderGenesis;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UniOrderGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UniOrderGenesisBlock;

            if(block.AccountType != LyraGlobal.GetAccountTypeFromTicker(block.Order.offering, block.Order.dir))
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "UniOrderGenesisAuthorizer->DaoAuthorizer");
        }
    }
}
