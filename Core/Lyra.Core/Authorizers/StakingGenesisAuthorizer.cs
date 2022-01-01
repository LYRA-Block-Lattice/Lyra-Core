using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class StakingGenesisAuthorizer : StakingAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is StakingGenesis))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as StakingGenesis;

            if (block.AccountType != AccountTypes.Staking)
                return APIResultCodes.InvalidBlockType;

            TransactionBlock lastBlock = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
            if (block.Height > 1 && lastBlock == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            // related tx must exist 
            var send = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (send == null || send.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                return APIResultCodes.InvalidMessengerAccount;

            // first verify account id
            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{block.Voting},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            if (block.AccountID != AccountId)
                return APIResultCodes.InvalidAccountId;

            return await MeasureAuthAsync("StakingGenesisAuthorizer", "StakingAuthorizer", base.AuthorizeImplAsync(sys, tblock));
        }
    }
}
