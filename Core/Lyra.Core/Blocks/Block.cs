using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.API;
using Lyra.Core.Cryptography;
using Lyra.Core.Decentralize;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Newtonsoft.Json;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public abstract class Block: SignableObject
    {
        /// <summary>
        /// Universal Index. Generated only by leader node.
        /// </summary>
        public long UIndex { get; set; }

        public long Index { get; set; }

        public DateTime TimeStamp { get; set; }

        public int Version { get; set; }

        public BlockTypes BlockType { get; set; }

        public string PreviousHash { get; set; }

        /// <summary>
        /// Custom metadata in key/value format.
        /// </summary>
        // TO DO there should be additional fee for using Tags based on size in bytes.
        public Dictionary<string, string> Tags { get; set; }

        public virtual BlockTypes GetBlockType() { return BlockTypes.Null; }

        public List<AuthorizationSignature> Authorizations { get; set; }

        public async virtual Task InitializeBlock(Block prevBlock, string PrivateKey, string AccountId, LyraRestClient client)
        {
            if (prevBlock != null)
            {
                Index = prevBlock.Index + 1;
                PreviousHash = prevBlock.Hash;
            }
            else
            {
                Index = 1;
                PreviousHash = null;//string.Empty;
            }
            TimeStamp = DateTime.Now.ToUniversalTime();
            Version = LyraGlobal.DatabaseVersion; // to do: change to global constant; should be used to fork the network; should be validated by comparing with the Node Version (taken from teh same globla contstant)
            BlockType = GetBlockType();

            // assign UID from seed0
            var uidResult = await client.CreateBlockUId(AccountId,
                Signatures.GetSignature(PrivateKey, Hash, AccountId),
                Hash);
            if (uidResult.ResultCode != APIResultCodes.Success)
            {
                return;
            }
            UIndex = uidResult.uid;

            Sign(PrivateKey, AccountId);
        }

        public override string GetHashInput()
        {
            return UIndex + "|" +
                this.Index.ToString() + "|" +
                             DateTimeToString(TimeStamp) + "|" +
                             this.Version + "|" +
                             this.BlockType.ToString() + "|" +
                             this.PreviousHash + "|" +
                             JsonConvert.SerializeObject(Tags) + "|" +
                             this.GetExtraData();
        }

        // should be overriden in specific instance to get the correct hash claculated from the entire block data 
        protected override string GetExtraData()
        {
            return string.Empty;
        }

        // Check if the block is valid by comparing its parameters with the onces from the previous block
        public virtual bool IsBlockValid(Block prevBlock)
        {
            // *** All blocks except for account opening ones must have a previous block
            if (!(this is IOpeningBlock))
            {
                if (string.IsNullOrWhiteSpace(this.PreviousHash))
                    return false;

                if (prevBlock == null)
                    return false;

                if (prevBlock.Index + 1 != this.Index)
                    return false;

                if (prevBlock.Hash != this.PreviousHash)
                    return false;
            }
            else
            {
                if (this.Index != 1) // always 1 for open block
                    return false;
            }

            if (!ValidateTags())
                return false;

            //if (!VerifyHash())
            //    return false;

            return true;
        }

        protected const int MAX_TAGS_COUNT = 16;

        protected const int MAX_STRING_LENGTH = 256;

        protected virtual bool ValidateTags()
        {
            if (Tags == null)
                return true;

            if (Tags.Count > MAX_TAGS_COUNT)
                throw new ApplicationException("Too many tags");

            foreach (var tag in Tags)
            {
                if (string.IsNullOrEmpty(tag.Value) && tag.Value.Length > MAX_STRING_LENGTH)
                    throw new ApplicationException("Tag value is too long");

                if (string.IsNullOrEmpty(tag.Key) && tag.Key.Length > MAX_STRING_LENGTH)
                    throw new ApplicationException("Tag key is too long");
            }

            return true;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"UIndex: {UIndex.ToString()}\n";
            result += $"Index: {Index.ToString()}\n";
            result += $"TimeStamp: {DateTimeToString(TimeStamp)}\n"; 
            result += $"Version: {Version}\n";
            result += $"BlockType: {BlockType.ToString()}\n";
            result += $"PreviousHash: {PreviousHash}\n";
            result += $"Tags: {JsonConvert.SerializeObject(Tags)}\n";
            result += $"Authorizations: {JsonConvert.SerializeObject(Authorizations)}\n";
            return result;
        }

    }

    public enum BlockTypes : byte
    {
        Null = 0,

        NullTransaction = 1,

        // Network service blocks

        //ServiceGenesis = 10,

        Service = 11,

        Consolidation = 12,

        Sync = 13,

        // Opening blocks

        // This is the very first block that creates Lyra Gas token on primary shard
        LyraTokenGenesis = 20,

        // account opening block where the first transaction is receive transfer
        OpenAccountWithReceiveTransfer = 21,

        // the same as OpenWithReceiveTransfer Block but tells the authorizer that it received fee instead of regular transfer
        OpenAccountWithReceiveFee = 22,

        // Open a new account and import another account
        OpenAccountWithImport = 23,

        // Transaction blocks

        TokenGenesis = 30,

        SendTransfer = 31,

        ReceiveTransfer = 32,

        // adds tarnsfers' fee to authorizer's account, 
        // the fee is settled when a new sync or service block is generated, for the previous service Index, 
        // by summarizing all the fee amounts from all blocks with the same corresponding sefrviceblock hash and dividing it by the number of authorizers in the sample,
        // the block can be validated by the next sample and all other nores in the same way,
        // fee data is not encrypted 
        ReceiveFee = 33,

        // Imports an account into current account
        ImportAccount = 34,

        ReceiveMultipleFee = 35,
        // Trading blocks

        // Put Sell or Buy trade order to exchange tokens
        TradeOrder = 40,

        // Send tokens to the trade order to initiate trade
        Trade = 41,

        // Exchange tokens with Trade initiator to conclude the trade and execute the trade order
        ExecuteTradeOrder = 42,

        // Cancels the order and frees up the locked funds
        CancelTradeOrder = 43,

        // to/from exchange
        ExchangingTransfer = 50,
    }

    public enum APIResultCodes
    {
        Success = 0,
        UnknownError = 1,
        // default error code
        UndefinedError = 1000,
        BlockWithThisUIndexAlreadyExists = 1001,
        BlockWithThisIndexAlreadyExists = 2,
        AccountAlreadyExists = 3,
        AccountDoesNotExist = 4,
        BlockWithThisPreviousHashAlreadyExists = 5, // double-spending attempt - trying to add another block to the same previous block
        BlockValidationFailed = 6,
        TokenGenesisBlockAlreadyExists = 7,
        CouldNotFindLatestBlock = 8,
        NegativeTransactionAmount = 9,
        AccountChainBlockValidationFailed = 10,
        AccountChainSignatureValidationFailed = 11,
        AccountChainBalanceValidationFailed = 12,
        AccountBlockAlreadyExists = 13,
        SourceSendBlockNotFound = 14,
        InvalidDestinationAccountId = 15,
        CouldNotTraceSendBlockChain = 16,
        TransactionAmountDoesNotMatch = 17,
        ExceptionInOpenAccountWithGenesis = 18,
        ExceptionInSendTransfer = 19,
        ExceptionInReceiveTransferAndOpenAccount = 20,
        ExceptionInReceiveTransfer = 21,
        InvalidBlockType = 22,
        ExceptionInCreateToken = 23,
        InvalidFeeAmount = 24,
        InvalidNewAccountBalance = 25,
        SendTransactionValidationFailed = 26,
        ReceiveTransactionValidationFailed = 27,
        TransactionTokenDoesNotMatch = 28,
        BlockSignatureValidationFailed = 29,
        NoNewTransferFound = 30,
        TokenGenesisBlockNotFound = 31,
        ServiceBlockNotFound = 32,
        BlockNotFound = 33,
        NoRPCServerConnection = 34,
        ExceptionInNodeAPI = 35,
        ExceptionInWebAPI = 36,
        PreviousBlockNotFound = 37,
        InsufficientFunds = 38,
        InvalidAccountId = 39,
        InvalidPrivateKey = 40,
        TradeOrderMatchFound = 41,
        InvalidIndexSequence = 42,
        FeatureIsNotSupported = 48,

        // Trade Codes

        ExceptionInTradeOrderAuthorizer = 43,
        ExceptionInTradeAuthorizer = 44,
        ExceptionInExecuteTradeOrderAuthorizer = 45,
        ExceptionInCancelTradeOrderAuthorizer = 46,

        TradeOrderValidationFailed = 47,
        NoTradesFound = 49,
        TradeOrderNotFound = 50,
        InvalidTradeAmount = 51,

        // Non-fungible token codes
        InvalidNonFungibleAmount = 52,
        InvalidNonFungibleTokenCode = 53,
        MissingNonFungibleToken = 54,
        InvalidNonFungibleSenderAccountId = 55,
        NoNonFungibleTokensFound = 56,
        OriginNonFungibleBlockNotFound = 57,
        SourceNonFungibleBlockNotFound = 58,
        OriginNonFungibleBlockHashDoesNotMatch = 59,
        SourceNonFungibleBlockHashDoesNotMatch = 60,
        NonFungibleSignatureVerificationFailed = 61,
        InvalidNonFungiblePublicKey = 62,

        CancelTradeOrderValidationFailed = 63,

        InvalidFeeType = 64,

        InvalidParameterFormat = 65,

        APISignatureValidationFailed = 66,

        InvalidNetworkId = 67,

        // service blocks related
        InvalidConsolidationMerkleTreeHash,

        DuplicateReceiveBlock = 100,

        InvalidTokenRenewalDate = 200,

        TokenExpired = 201,

        NameUnavailable = 202,

        NotAllowedToSign = 300,
        NotAllowedToCommit = 301,
        UnableToSendToConsensusNetwork = 302,
        NodeOutOfSync = 303,
        PBFTNetworkNotReadyForConsensus = 304,
        DoubleSpentDetected = 305,
        NotListedAsQualifiedAuthorizer = 306
    }
}
