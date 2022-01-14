using Lyra.Core.Blocks;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class OTCCryptoOrderRecvAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCCryptoOrderRecv;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OtcCryptoOrderRecvBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OtcCryptoOrderRecvBlock;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (relTx == null || relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            {
                // verify its pf or dao
                var daog = await sys.Storage.FindFirstBlockAsync(relTx.DestinationAccountId) as DaoGenesisBlock;
                if(daog == null && relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                    return APIResultCodes.InvalidServiceRequest;
            }

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

    public class OTCCryptoOrderSendAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCCryptoOrderSend;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OtcCryptoOrderSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OtcCryptoOrderSendBlock;

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

    public class OTCCryptoOrderGenesisAuthorizer : OTCCryptoOrderRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCCryptoOrderGenesis;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OTCCryptoOrderGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OTCCryptoOrderGenesisBlock;

            if(block.AccountType != AccountTypes.OTC)
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DaoGenesisAuthorizer->DaoAuthorizer");
        }
    }
}
