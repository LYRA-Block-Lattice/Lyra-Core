//using System.Collections.Generic;
//using Lyra.Core.Blocks;

//using Lyra.Core.Cryptography;
//using Lyra.Core.API;

//namespace Lyra.Node
//{
//    public class Authorizer
//    {
//        readonly ServiceAccount _serviceAccount;
//        readonly LiteAccountCollection _accountCollection;

//        //public Authorizer(ServiceAccount serviceAccount, AccountCollection accountCollection)
//        public Authorizer(ServiceAccount serviceAccount, LiteAccountCollection accountCollection)
//        {
//            _serviceAccount = serviceAccount;
//            _accountCollection = accountCollection;
//        }

//        public APIResultCodes AuthorizeFirstGenesisBlock(ref FirstGenesisBlock openTokenGenesisBlock)
//        {
//            APIResultCodes result;

//            // Local node validations - before it sends it out to the authorization sample:
//            // 1. check if the account already exists
//            if (_accountCollection.AccountExists(openTokenGenesisBlock.AccountID))
//                return APIResultCodes.AccountAlreadyExists; // 

//            // 2. Validate blocks
//            result = VerifyBlock(openTokenGenesisBlock, null);
//            if (result != APIResultCodes.Success)
//                return result;

//            // check if this token already exists
//            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
//            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
//            if (_accountCollection.FindTokenGenesisBlock(openTokenGenesisBlock.Hash, openTokenGenesisBlock.Ticker) != null)
//                return APIResultCodes.TokenGenesisBlockAlreadyExists;

//            // sign with the authorizer key
//            Sign(openTokenGenesisBlock);
//            Sign(openTokenGenesisBlock);

//            return APIResultCodes.Success;
//        }

//        public APIResultCodes AuthorizeNewToken(ref TokenGenesisBlock tokenBlock)
//        {
//            APIResultCodes result;

//            // Local node validations - before it sends it out to the authorization sample:
//            // 1. check if the account already exists
//            if (!_accountCollection.AccountExists(tokenBlock.AccountID))
//                return APIResultCodes.AccountDoesNotExist; // 

//            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(tokenBlock.AccountID);
//            if (lastBlock == null)
//                return APIResultCodes.CouldNotFindLatestBlock;

//            // 2. Validate blocks
//            result = VerifyBlock(tokenBlock, lastBlock);
//            if (result != APIResultCodes.Success)
//                return result;

//            result = VerifyTransactionBlock(tokenBlock);
//            if (result != APIResultCodes.Success)
//                return result;

//            // check LYR balance
//            if (lastBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] != tokenBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] + tokenBlock.Fee)
//                return APIResultCodes.InvalidNewAccountBalance;

//            // check if this token already exists
//            //AccountData genesis_blocks = _accountCollection.GetAccount(AccountCollection.GENESIS_BLOCKS);
//            //if (genesis_blocks.FindTokenGenesisBlock(testTokenGenesisBlock) != null)
//            if (_accountCollection.FindTokenGenesisBlock(tokenBlock.Hash, tokenBlock.Ticker) != null)
//                return APIResultCodes.TokenGenesisBlockAlreadyExists;

//            // sign with the authorizer key
//            Sign(tokenBlock);

//            return APIResultCodes.Success;
//        }


//        public APIResultCodes AuthorizeSendTransferBlock(ref SendTransferBlock block)
//        {
//            APIResultCodes result;

//            // 1. check if the account already exists
//            if (!_accountCollection.AccountExists(block.AccountID))
//                return APIResultCodes.AccountDoesNotExist;

//            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(block.AccountID);
//            if (lastBlock == null)
//                return APIResultCodes.CouldNotFindLatestBlock;

//            result = VerifyBlock(block, lastBlock);
//            if (result != APIResultCodes.Success)
//                return result;

//            //if (lastBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] <= block.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] + block.Fee)
//            //    return AuthorizationResultCodes.NegativeTransactionAmount;

//            // Validate the destination account id
//            if (!Signatures.ValidateKey(block.DestinationAccountId))
//                return APIResultCodes.InvalidDestinationAccountId;

//            result = VerifyTransactionBlock(block);
//            if (result != APIResultCodes.Success)
//                return result;

//            if (!block.ValidateTransaction(lastBlock))
//                return APIResultCodes.SendTransactionValidationFailed;

//            Sign(block);

//            return APIResultCodes.Success;
//        }

//        public APIResultCodes AuthorizeReceiveBlockAndNewAccount(ref OpenWithReceiveTransferBlock block)
//        {
//            APIResultCodes result;

//            // 1. check if the account already exists
//            if (_accountCollection.AccountExists(block.AccountID))
//                return APIResultCodes.AccountAlreadyExists;

//            // This is redundant but just in case
//            if (_accountCollection.FindLatestBlock(block.AccountID) != null)
//                return APIResultCodes.AccountBlockAlreadyExists;

//            result = VerifyBlock(block, null);
//            if (result != APIResultCodes.Success)
//                return result;

//            result = VerifyTransactionBlock(block);
//            if (result != APIResultCodes.Success)
//                return result;

//            result = ValidateReceiveTransAmount(block, block.GetTransaction(null));
//            if (result != APIResultCodes.Success)
//                return result;

//            Sign(block);

//            return APIResultCodes.Success;
//        }

//        public APIResultCodes AuthorizeReceiveBlock(ref ReceiveTransferBlock block)
//        {
//            APIResultCodes result;

//            // 1. check if the account already exists
//            if (!_accountCollection.AccountExists(block.AccountID))
//                return APIResultCodes.AccountDoesNotExist;

//            TransactionBlock lastBlock = _accountCollection.FindLatestBlock(block.AccountID);
//            if (lastBlock == null)
//                return APIResultCodes.CouldNotFindLatestBlock;

//            result = VerifyBlock(block, lastBlock);
//            if (result != APIResultCodes.Success)
//                return result;

//            //if (lastBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] > block.Balances[TokenGenesisBlock.LYRA_TICKER_CODE])
//            //    return AuthorizationResultCodes.NegativeTransactionAmount;

//            result = VerifyTransactionBlock(block);
//            if (result != APIResultCodes.Success)
//                return result;


//            if (!block.ValidateTransaction(lastBlock))
//                return APIResultCodes.ReceiveTransactionValidationFailed;

//            result = ValidateReceiveTransAmount(block, block.GetTransaction(lastBlock));
//            if (result != APIResultCodes.Success)
//                return result;

//            Sign(block);

//            return APIResultCodes.Success;
//        }

//        APIResultCodes ValidateReceiveTransAmount(ReceiveTransferBlock block, TransactionInfo receiveTransaction)
//        {
//            //find the corresponding send block and validate the added transaction amount
//            var sourceBlock = _accountCollection.FindBlockByHash(block.SourceHash);
//            if (sourceBlock == null)
//                return APIResultCodes.SourceSendBlockNotFound;


//            // find the actual amount of transaction 

//            TransactionBlock prevToSendBlock = _accountCollection.FindBlockByHash(sourceBlock.PreviousHash);
//            if (prevToSendBlock == null)
//                return APIResultCodes.CouldNotTraceSendBlockChain;


//            TransactionInfo sendTransaction;
//            if (block.BlockType == BlockTypes.ReceiveTransfer || block.BlockType == BlockTypes.OpenWithReceiveTransfer)
//            {
//                if ((sourceBlock as SendTransferBlock).DestinationAccountId != block.AccountID)
//                    return APIResultCodes.InvalidDestinationAccountId;

//                sendTransaction = sourceBlock.GetTransaction(prevToSendBlock);

//                if (!sourceBlock.ValidateTransaction(prevToSendBlock))
//                    return APIResultCodes.SendTransactionValidationFailed;
//                //originallySentAmount = sendTransaction.Amount;
//                //originallySentAmount = 
//                //    prevToSendBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] - sourceBlock.Balances[TokenGenesisBlock.LYRA_TICKER_CODE] - (sourceBlock as IFeebleBlock).Fee;
//            }
//            else
//            if (block.BlockType == BlockTypes.ReceiveFee || block.BlockType == BlockTypes.OpenWithReceiveFee)
//            {
//                sendTransaction = new TransactionInfo() { TokenCode = TokenGenesisBlock.LYRA_TICKER_CODE, Amount = (sourceBlock as IFeebleBlock).Fee };
//            }
//            else
//                return APIResultCodes.InvalidBlockType;

//            if (sendTransaction.Amount != receiveTransaction.Amount)
//                return APIResultCodes.TransactionAmountDoesNotMatch;

//            if (sendTransaction.TokenCode != receiveTransaction.TokenCode)
//                return APIResultCodes.TransactionTokenDoesNotMatch;

//            return APIResultCodes.Success;
//        }

//        private APIResultCodes VerifyBlock(TransactionBlock block, TransactionBlock prviousBlock)
//        {
//            if (!block.IsBlockValid(prviousBlock))
//                return APIResultCodes.BlockValidationFailed;

//            if (!Signatures.VerifySignature(block.Hash, block.AccountID, block.Signature))
//                return APIResultCodes.BlockSignatureValidationFailed;

//            // check if this Index already exists (double-spending, kind of)
//            if (_accountCollection.FindBlockByIndex(block.AccountID, block.Index) != null)
//                return APIResultCodes.BlockWithThisIndexAlreadyExists;

//            // This is the double-spending check for send block!
//            if (!string.IsNullOrEmpty(block.PreviousHash) && _accountCollection.FindBlockByPreviousBlockHash(block.PreviousHash) != null)
//                return APIResultCodes.BlockWithThisPreviousHashAlreadyExists;

//            return APIResultCodes.Success;
//        }

//        // common validations for Send and Receive blocks
//        private APIResultCodes VerifyTransactionBlock(TransactionBlock block)
//        {
//            // Validate the account id
//            if (!Signatures.ValidateKey(block.AccountID))
//                return APIResultCodes.InvalidAccountId;

//            if (!string.IsNullOrEmpty(block.PreviousHash)) // not for new account
//            {
//                // verify the entire account chain to make sure all account's blocks are valid
//                TransactionBlock prevBlock, thisBlock = block;
//                //while (thisBlock.BlockType != BlockTypes.OpenWithReceiveTransfer && thisBlock.BlockType != BlockTypes.OpenWithReceiveFee)
//                while (!(thisBlock is IOpeningBlock))
//                {
//                    prevBlock = _accountCollection.FindBlockByHash(thisBlock.PreviousHash);
//                    if (!thisBlock.IsBlockValid(prevBlock))
//                        return APIResultCodes.AccountChainBlockValidationFailed;

//                    if (!Signatures.VerifySignature(thisBlock.Hash, thisBlock.AccountID, thisBlock.Signature))
//                        return APIResultCodes.AccountChainSignatureValidationFailed;

//                    thisBlock = prevBlock;
//                }

//                // verify the spending
//                TransactionBlock previousTransaction = _accountCollection.FindBlockByHash(block.PreviousHash);
//                foreach (var prevbalance in previousTransaction.Balances)
//                {
//                    // make sure all balances from the previous block are present in a new block even if they are unchanged
//                    if (!block.Balances.ContainsKey(prevbalance.Key))
//                        return APIResultCodes.AccountChainBalanceValidationFailed;
//                }

//                // Verify fee
//                if (block.BlockType == BlockTypes.SendTransfer)
//                    if ((block as SendTransferBlock).Fee != _serviceAccount.GetLastServiceBlock().TransferFee)
//                        return APIResultCodes.InvalidFeeAmount;

//                if (block.BlockType == BlockTypes.Genesis)
//                    if ((block as TokenGenesisBlock).Fee != _serviceAccount.GetLastServiceBlock().TokenGenerationFee)
//                        return APIResultCodes.InvalidFeeAmount;
//            }

//            return APIResultCodes.Success;
//        }



//        private void Sign(TransactionBlock block)
//        {
//            // sign with the authorizer key
//            AuthorizationSignature authSignature = new AuthorizationSignature
//            {
//                Key = _serviceAccount.AccountId,
//                Signature = Signatures.GetSignature(_serviceAccount.PrivateKey, block.Hash)
//            };

//            if (block.Authorizations == null)
//                block.Authorizations = new List<AuthorizationSignature>();
//            block.Authorizations.Add(authSignature);
//        }

//    }
//}
