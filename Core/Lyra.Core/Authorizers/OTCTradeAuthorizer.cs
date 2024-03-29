﻿using Lyra.Core.Blocks;
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
    public class OTCTradeResolution : OTCTradeRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCTradeResolutionRecv;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OtcVotedResolutionBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OtcVotedResolutionBlock;
            var vs = await sys.Storage.GetVoteSummaryAsync(block.voteid);
            if (vs == null || !vs.IsDecided)
                return APIResultCodes.InvalidVote;

            return await base.AuthorizeImplAsync<T>(sys, tblock);
        }
    }


    public class OTCTradeRecvAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCTradeRecv;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OtcTradeRecvBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OtcTradeRecvBlock;

            if (block.Trade.price.CountDecimalDigits() > 8 ||
                block.Trade.amount.CountDecimalDigits() > 8 ||
                block.Trade.collateral.CountDecimalDigits() > 8 ||
                block.Trade.pay.CountDecimalDigits() > 8
                )
                return APIResultCodes.InvalidDecimalDigitalCount;

            //// related tx must exist 
            //var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            //if (relTx == null || relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            //{
            //    // verify its pf or dao
            //    var dao = await sys.Storage.FindFirstBlockAsync(relTx.DestinationAccountId);
            //    var daog = dao as DaoGenesisBlock; //xx no, this is dest the trade chain
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

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "OTCTradeRecvAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class OTCTradeSendAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCTradeSend;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OtcTradeSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OtcTradeSendBlock;

            if (block.Trade.price.CountDecimalDigits() > 8 ||
                block.Trade.amount.CountDecimalDigits() > 8 ||
                block.Trade.collateral.CountDecimalDigits() > 8 ||
                block.Trade.pay.CountDecimalDigits() > 8
                )
                return APIResultCodes.InvalidDecimalDigitalCount;

            //// related tx must exist 
            //var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            //if (relTx == null || relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            //{
            //    // verify its pf or dao
            //    var daog = await sys.Storage.FindFirstBlockAsync(relTx.DestinationAccountId) as DaoGenesisBlock;
            //    if (daog == null && relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
            //        return APIResultCodes.InvalidServiceRequest;
            //}

            // service must not been processed
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

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "OTCTradeSendAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class OTCTradeGenesisAuthorizer : OTCTradeRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OTCTradeGenesis;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OtcTradeGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as OtcTradeGenesisBlock;

            if(block.AccountType != AccountTypes.OTC)
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "OTCTradeGenesisAuthorizer->DaoAuthorizer");
        }
    }
}
