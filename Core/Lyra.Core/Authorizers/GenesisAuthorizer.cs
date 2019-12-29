using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Lyra.Core.Accounts.Node;
using Lyra.Core.API;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class GenesisAuthorizer: BaseAuthorizer
    {
        public GenesisAuthorizer(IOptions<LyraNodeConfig> config, ServiceAccount serviceAccount, IAccountCollection accountCollection)
            : base(config, serviceAccount, accountCollection)
        {
        }

        public override Task<(APIResultCodes, AuthorizationSignature)> Authorize<T>(T tblock)
        {
            var result = AuthorizeImpl(tblock);
            if (APIResultCodes.Success == result)
                return Task.FromResult((APIResultCodes.Success, Sign(tblock)));
            else
                return Task.FromResult((result, (AuthorizationSignature)null));
        }
        private APIResultCodes AuthorizeImpl<T>(T tblock)
        {
            if (!(tblock is LyraTokenGenesisBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as LyraTokenGenesisBlock;

            if ((block as LyraTokenGenesisBlock).Ticker != LyraGlobal.LYRA_TICKER_CODE)
                return APIResultCodes.InvalidBlockType;

            // Local node validations - before it sends it out to the authorization sample:
            // 1. check if the account already exists
                if (_accountCollection.AccountExists(block.AccountID))
                return APIResultCodes.AccountAlreadyExists; // 

            // 2. Validate blocks
            var result = VerifyBlock(block, null);
            if (result != APIResultCodes.Success)
                return result;

            // check if this token already exists
            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
            if (_accountCollection.FindTokenGenesisBlock(block.Hash, LyraGlobal.LYRA_TICKER_CODE) != null)
                return APIResultCodes.TokenGenesisBlockAlreadyExists;

            return APIResultCodes.Success;
        }

        protected override APIResultCodes ValidateFee(TransactionBlock block)
        {
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                return APIResultCodes.InvalidFeeAmount;

            if (block.Fee != 0)
                return APIResultCodes.InvalidFeeAmount;

            return APIResultCodes.Success;
        }



    }
}
