using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using Lyra.Core.API;
using Lyra.Core.Decentralize;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Utils;
using Lyra.Core.Accounts;

namespace Lyra.Core.Authorizers
{
    public class ImportAccountAuthorizer : BaseAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            // this operation makes too much trouble. disable temproray
            return APIResultCodes.Unsupported;

            if (!(tblock is ImportAccountBlock))
                return APIResultCodes.InvalidBlockType;

            var import_block = tblock as ImportAccountBlock;

            // existing account must have a previous block
            TransactionBlock previous_block = await sys.Storage.FindLatestBlockAsync(import_block.AccountID) as TransactionBlock;
            if (previous_block == null)
                return APIResultCodes.CouldNotFindLatestBlock;

            TransactionBlock last_imported_block = await sys.Storage.FindLatestBlockAsync(import_block.ImportedAccountId) as TransactionBlock;

            var bret = await base.AuthorizeImplAsync(sys, tblock);
            if (bret != APIResultCodes.Success)
                return bret;

            return await ValidateImportedAccountAsync(sys, import_block, previous_block, last_imported_block);
        }

        protected async Task<APIResultCodes> ValidateImportedAccountAsync(DagSystem sys, ImportAccountBlock import_block, TransactionBlock previous_block, TransactionBlock last_imported_block)
        {
            if (import_block.AccountID == import_block.ImportedAccountId)
                return APIResultCodes.CannotImportAccountToItself;

            var result = await VerifyBlockAsync(sys, import_block, previous_block);
            if (result != APIResultCodes.Success)
                return result;

            // account could be imported already even if it does no exists!
            if (await sys.Storage.WasAccountImportedAsync(import_block.AccountID))
                return APIResultCodes.CannotModifyImportedAccount;

            // account could be imported already even if it does no exists!
            if (await sys.Storage.WasAccountImportedAsync(import_block.ImportedAccountId))
                return APIResultCodes.AccountAlreadyImported;

            // we use ImportedLastBlockHash instead to identify the balances that are being imported
            if (!string.IsNullOrEmpty(import_block.SourceHash))
                return APIResultCodes.ImportTransactionValidationFailed;

            // there must be pending send TX for imported account out there, or account must have already balance; otherwise they may use ImportAccount for DOS attack
            bool imported_account_exists = last_imported_block != null;
            if (!imported_account_exists)
            {
                var pending_send_block = await sys.Storage.FindUnsettledSendBlockByDestinationAccountIdAsync(import_block.ImportedAccountId);
                if (pending_send_block == null) // this is fake imported account
                    return APIResultCodes.CannotImportEmptyAccount;
            }
            else
            {
                // Cannot import account that already contains other imported accounts
                var import_blocks = await sys.Storage.GetImportedAccountBlocksAsync(import_block.ImportedAccountId);
                if (import_blocks != null && import_blocks.Count > 0)
                    return APIResultCodes.CannotImportAccountWithOtherImports;
            }

            result = ValidateImportedBalances(sys, import_block, previous_block, last_imported_block);
            if (result != APIResultCodes.Success)
                return result;

            result = await VerifyTransactionBlockAsync(sys, import_block);
            if (result != APIResultCodes.Success)
                return result;

            result = await ValidateNonFungibleAsync(sys, import_block, previous_block);
            if (result != APIResultCodes.Success)
                return result;


            return APIResultCodes.Success;
        }

        protected virtual APIResultCodes ValidateImportedBalances(DagSystem sys, ImportAccountBlock import_block, TransactionBlock previous_block, TransactionBlock last_imported_block)
        {
            bool imported_account_exists = last_imported_block != null;
            if (!imported_account_exists)
            {
                // if imported account is empty, all the balances should be simply copied from the previous block of the target account
                if (import_block.Balances.Count != previous_block.Balances.Count)
                    return APIResultCodes.ImportTransactionValidationFailed;

                foreach (var balance in import_block.Balances)
                    if (previous_block.Balances[balance.Key] != balance.Value)
                        return APIResultCodes.ImportTransactionValidationFailed;
            }
            else
            {
                if (import_block.ImportedLastBlockHash != last_imported_block.Hash)
                    return APIResultCodes.ImportTransactionValidationFailed;

                // both imported and target account already have transaction, so let's validate that the import block contains merged balances
                foreach (var balance in import_block.Balances)
                {
                    if (!previous_block.Balances.ContainsKey(balance.Key) && !last_imported_block.Balances.ContainsKey(balance.Key))
                            return APIResultCodes.ImportTransactionValidationFailed;

                    long previous_value =0, last_imported_value = 0;
                    if (previous_block.Balances.ContainsKey(balance.Key))
                        previous_value = previous_block.Balances[balance.Key];
                    if (last_imported_block.Balances.ContainsKey(balance.Key))
                        last_imported_value = last_imported_block.Balances[balance.Key];

                    if ((previous_value + last_imported_value) != balance.Value)
                        return APIResultCodes.ImportTransactionValidationFailed;
                }
            }
            return APIResultCodes.Success;
        }

        protected override async Task<APIResultCodes> ValidateNonFungibleAsync(DagSystem sys, TransactionBlock send_or_receice_block, TransactionBlock previousBlock)
        {
            await Task.Run(() => { });
            if (send_or_receice_block.ContainsNonFungibleToken())
                return APIResultCodes.ImportTransactionValidationFailed;
            return APIResultCodes.Success;
        }

        protected override Task<APIResultCodes> ValidateFeeAsync(DagSystem sys, TransactionBlock block)
        {
            var result = APIResultCodes.Success;
            
            if (block.FeeType != AuthorizationFeeTypes.NoFee)
                result = APIResultCodes.InvalidFeeAmount;

            if (block.Fee != 0)
                result = APIResultCodes.InvalidFeeAmount;

            return Task.FromResult(result);
        }

    }
}
