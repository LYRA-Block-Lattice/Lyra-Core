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
    public class DaoAuthorizer : BrokerAccountRecvAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DaoBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DaoBlock;

            // related tx must exist 
            var relTx = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (relTx == null || relTx.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                return APIResultCodes.InvalidServiceRequest;

            // service must not been processed
            var processed = await sys.Storage.FindBlocksByRelatedTxAsync(block.RelatedTx);
            if (processed.Count != 0)
                return APIResultCodes.InvalidServiceRequest;

            var name = relTx.Tags["name"];

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{relTx.Hash.Substring(0, 16)},{name},{relTx.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            if (block.AccountID != AccountId)
                return APIResultCodes.InvalidAccountId;

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DaoAuthorizer->BrokerAccountRecvAuthorizer");
        }
    }

    public class DaoGenesisAuthorizer : DaoAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is DaoGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as DaoGenesisBlock;

            if(block.AccountType != AccountTypes.DAO)
            {
                return APIResultCodes.InvalidAccountType;
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "DaoGenesisAuthorizer->DaoAuthorizer");
        }
    }
}
