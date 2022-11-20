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
    public class DaoVoteExecAuthorizer : DaoRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationChange;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DaoVotedChangeBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DaoVotedChangeBlock;
            var vs = await sys.Storage.GetVoteSummaryAsync(block.voteid);
            if(vs == null || !vs.IsDecided)
                return APIResultCodes.InvalidVote;

            return await base.AuthorizeImplAsync<T>(sys, tblock);
        }
    }
    public class DaoRecvAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationRecv;
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

            if (relTx.DestinationAccountId != LyraGlobal.GUILDACCOUNTID)
            {
                // verify its pf or dao
                var daog = await sys.Storage.FindFirstBlockAsync(relTx.DestinationAccountId) as DaoGenesisBlock;
                if(daog == null && relTx.DestinationAccountId != LyraGlobal.GUILDACCOUNTID)
                    return APIResultCodes.InvalidServiceRequest;
            }

            // service must not been processed, no, at least there is a receive block
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

    public class DaoSendAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationSend;
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

    public class DaoGenesisAuthorizer : DaoRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OrgnizationGenesis;
        }

        //static int count = 0;

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            //if(count == 0)
            //{
            //    await Task.Delay(16000);
            //    count++;
            //}                

            if (!(tblock is DaoGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DaoGenesisBlock;

            if(block.AccountType != AccountTypes.DAO && block.AccountType != AccountTypes.Guild)
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DaoGenesisAuthorizer->DaoAuthorizer");
        }
    }
}
