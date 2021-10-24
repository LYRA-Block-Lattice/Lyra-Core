using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class ProfitingGenesisAuthorizer : ProfitingAuthorizer
    {
        public override async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock)
        {
            var br = await base.AuthorizeAsync(sys, tblock);
            APIResultCodes result;
            if (br.Item1 == APIResultCodes.Success)
                result = await AuthorizeImplAsync(sys, tblock);
            else
                result = br.Item1;

            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }
        private async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ProfitingGenesis))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ProfitingGenesis;

            if (block.AccountType != AccountTypes.Profiting)
                return APIResultCodes.InvalidBlockType;

            var send = await sys.Storage.FindBlockByHashAsync(block.RelatedTx) as SendTransferBlock;
            if (send == null || send.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                return APIResultCodes.InvalidMessengerAccount;

            // first verify account id
            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{block.PType},{block.ShareRito.ToBalanceLong()},{block.Seats},{send.AccountID}";
            var (_, AccountId) = Signatures.GenerateWallet(Encoding.ASCII.GetBytes(keyStr).Take(32).ToArray());

            if (block.AccountID != AccountId)
                return APIResultCodes.InvalidAccountId;

            return APIResultCodes.Success;
        }
    }
}
