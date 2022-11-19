using Lyra.Core.API;
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
    public class GuildRecvAuthorizer : DaoRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.GuildRecv;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.Dynamic;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DaoRecvBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DaoRecvBlock;

            if (block.ShareRito.CountDecimalDigits() > 8 || block.SellerFeeRatio.CountDecimalDigits() > 8 || block.BuyerFeeRatio.CountDecimalDigits() > 8)
                return APIResultCodes.InvalidDecimalDigitalCount;

            // profiting
            if (block.ShareRito < 0 || block.ShareRito > 1)
                return APIResultCodes.InvalidShareRitio;

            if (block.Seats < 0 || block.Seats > 100)
                return APIResultCodes.InvalidSeatsCount;

            if (block.ShareRito == 0 && block.Seats != 0)
                return APIResultCodes.InvalidSeatsCount;

            if (block.ShareRito > 0 && block.Seats == 0)
                return APIResultCodes.InvalidSeatsCount;

            // dao
            if (block.PType != ProfitingType.Orgnization)
                return APIResultCodes.InvalidDataType;

            if (string.IsNullOrEmpty(block.Description) || block.Description.Length > 300)
                return APIResultCodes.ArgumentOutOfRange;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if(relTx == null)
                return APIResultCodes.InvalidServiceRequest;

            if (relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            {
                // verify its pf or dao
                var daog = await sys.Storage.FindFirstBlockAsync(relTx.DestinationAccountId) as DaoGenesisBlock;
                if(daog == null && relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                    return APIResultCodes.InvalidServiceRequest;
            }

            // service must not been processed
            var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            if (processed.Count != 0)
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

    public class GuildSendAuthorizer : DaoSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.GuildSend;
        }

        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.Dynamic;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DaoSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DaoSendBlock;

            if (block.ShareRito.CountDecimalDigits() > 8 || block.SellerFeeRatio.CountDecimalDigits() > 8 || block.BuyerFeeRatio.CountDecimalDigits() > 8)
                return APIResultCodes.InvalidDecimalDigitalCount;

            // profiting
            if (block.ShareRito < 0 || block.ShareRito > 1)
                return APIResultCodes.InvalidShareRitio;

            if (block.Seats < 0 || block.Seats > 100)
                return APIResultCodes.InvalidSeatsCount;

            if (block.ShareRito == 0 && block.Seats != 0)
                return APIResultCodes.InvalidSeatsCount;

            if (block.ShareRito > 0 && block.Seats == 0)
                return APIResultCodes.InvalidSeatsCount;

            // dao
            if (block.PType != ProfitingType.Orgnization)
                return APIResultCodes.InvalidDataType;

            if (string.IsNullOrEmpty(block.Description) || block.Description.Length > 300)
                return APIResultCodes.ArgumentOutOfRange;

            //// related tx must exist 
            //var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            //if (relTx == null || relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            //{
            //    // verify its pf or dao
            //    var daog = await sys.Storage.FindFirstBlockAsync(relTx.DestinationAccountId) as DaoGenesisBlock;
            //    if (daog == null && relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            //        return APIResultCodes.InvalidServiceRequest;
            //}

            //// service must not been processed
            //var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            //if (processed.Count != 1)
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

    public class GuildGenesisAuthorizer : DaoGenesisAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.GuildGenesis;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is GuildGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DaoGenesisBlock;

            if(block.AccountID != LyraGlobal.GUILDACCOUNTID)
                return APIResultCodes.InvalidBlockType;

            if(await sys.Storage.AccountExistsAsync(block.AccountID))
                return APIResultCodes.InvalidBlockType;

            if(block.AccountType != AccountTypes.Guild)
            {
                return APIResultCodes.InvalidAccountType;
            }

            return APIResultCodes.Success;
        }
    }
}
