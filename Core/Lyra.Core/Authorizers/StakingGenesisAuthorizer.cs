﻿using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class StakingGenesisAuthorizer : BaseAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var result = await AuthorizeImplAsync(sys, tblock);

            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is StakingGenesis))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as StakingGenesis;

            if (block.AccountType != AccountTypes.Staking)
                return APIResultCodes.InvalidBlockType;

            TransactionBlock lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
            if (block.Height > 1 && lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            // Validate blocks
            var result = await VerifyBlockAsync(sys, block, lastBlock);
            if (result != APIResultCodes.Success)
                return result;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx);
            if (tblock is SendTransferBlock && relTx == null)
                return APIResultCodes.InvalidServiceRequest;

            // send account must be current owner
            var send = relTx as SendTransferBlock;
            if(send.AccountID != block.OwnerAccountId)
                return APIResultCodes.InvalidServiceRequest;

            // service must not been processed
            var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            if(tblock is SendTransferBlock && processed != null)
                return APIResultCodes.InvalidServiceRequest;

            // first verify account id
            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{block.Voting},{send.AccountID}";
            var (_, AccountId) = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            if (block.AccountID != AccountId)
                return APIResultCodes.InvalidAccountId;

            return APIResultCodes.Success;
        }
    }
}