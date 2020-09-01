using System.Threading.Tasks;
using Lyra.Core.Blocks;
using Microsoft.Extensions.Options;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class NewAccountWithImportAuthorizer : ImportAccountAuthorizer
    {
        public NewAccountWithImportAuthorizer()
        {
        }
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is OpenAccountWithImportBlock))
                return APIResultCodes.InvalidBlockType;

            var import_block = tblock as OpenAccountWithImportBlock;

            // check if the account already exists
            if (await sys.Storage.AccountExistsAsync(import_block.AccountID))
                return APIResultCodes.AccountAlreadyExists;

            TransactionBlock last_imported_block = await sys.Storage.FindLatestBlockAsync(import_block.AccountID) as TransactionBlock;

            return await ValidateImportedAccountAsync(sys, import_block, null, last_imported_block);
        }

        protected override APIResultCodes ValidateImportedBalances(DagSystem sys, ImportAccountBlock import_block, TransactionBlock previous_block, TransactionBlock last_imported_block)
        {
            bool imported_account_exists = last_imported_block != null;
            if (!imported_account_exists)
            {
                // if imported account is empty, there should be no balances!
                if (import_block.Balances.Count != 0)
                    return APIResultCodes.ImportTransactionValidationFailed;
            }
            else
            {
                if (import_block.ImportedLastBlockHash != last_imported_block.Hash)
                    return APIResultCodes.ImportTransactionValidationFailed;

                // only imported account already has a balances, so let's validate that the import block contains the same balances
                foreach (var balance in import_block.Balances)
                {
                    if (!last_imported_block.Balances.ContainsKey(balance.Key))
                        return APIResultCodes.ImportTransactionValidationFailed;

                    if (last_imported_block.Balances[balance.Key] != balance.Value)
                        return APIResultCodes.ImportTransactionValidationFailed;
                }
            }
            return APIResultCodes.Success;
        }
    }
}
