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
    public class UniTradeResolution : UniTradeRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeResolutionRecv;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UniVotedResolutionBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UniVotedResolutionBlock;
            var vs = await sys.Storage.GetVoteSummaryAsync(block.voteid);
            if (vs == null || !vs.IsDecided)
                return APIResultCodes.InvalidVote;

            return await base.AuthorizeImplAsync<T>(sys, tblock);
        }
    }


    public class UniTradeRecvAuthorizer : BrokerAccountRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeRecv;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UniTradeRecvBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UniTradeRecvBlock;

            if (block.Trade.price.CountDecimalDigits() > 8 ||
                block.Trade.amount.CountDecimalDigits() > 8 ||
                block.Trade.cltamt.CountDecimalDigits() > 8 ||
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

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "UniTradeRecvAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class UniTradeSendAuthorizer : BrokerAccountSendAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeSend;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UniTradeSendBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UniTradeSendBlock;

            if (block.Trade.price.CountDecimalDigits() > 8 ||
                block.Trade.amount.CountDecimalDigits() > 8 ||
                block.Trade.cltamt.CountDecimalDigits() > 8 ||
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

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "UniTradeSendAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class UniTradeGenesisAuthorizer : UniTradeRecvAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.UniTradeGenesis;
        }

        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is UniTradeGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as UniTradeGenesisBlock;

            if (block.AccountType != LyraGlobal.GetAccountTypeFromTicker(block.Trade.offering))
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "UniTradeGenesisAuthorizer->UniTradeRecvAuthorizer");
        }
    }
}
