using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Converto;
using Lyra.Core.API;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Newtonsoft.Json;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public abstract class Block: SignableObject
    {
        /// <summary>
        /// a tag indicate that the signature is created by current leader, not the private key owner.
        /// </summary>
        public const string MANAGEDTAG = "managed";

        /// <summary>
        /// a tag indicate that the block is to a managed account and need to be processed by leader/consensus network.
        /// "" -> create pool [to factory] or deposit funds [to pool]
        /// "withdraw" -> [to factory] withdraw funds from pool. 
        /// </summary>
        public const string REQSERVICETAG = "svcreq";

        // block data
        public long Height { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonRepresentation(BsonType.Document)]
        public DateTime TimeStamp { get; set; }

        public int Version { get; set; }

        public BlockTypes BlockType { get; set; }

        public string? PreviousHash { get; set; }
        
        public string ServiceHash { get; set; }

        /// <summary>
        /// Custom metadata in key/value format.
        /// </summary>
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, string>? Tags { get; set; }

        protected virtual BlockTypes GetBlockType() { return BlockTypes.Null; }

        public Block()
        {
            TimeStamp = DateTime.UtcNow;
        }

        public virtual T GenInc<T>() where T : Block
        {
            return this.ConvertTo<T>()  //gender change
                .With(new
                {
                    // most necessary!
                    Hash = "",
                    Signature = "",
                    Height = 0,
                    TimeStamp = DateTime.UtcNow,
                });
        }

        public void InitializeBlock(Block prevBlock, string PrivateKey, string AccountId)
        {
            if (prevBlock != null)
            {
                Height = prevBlock.Height + 1;
                PreviousHash = prevBlock.Hash;

                if (prevBlock.Hash != prevBlock.CalculateHash())
                    throw new Exception("Invalid previous block, possible data tampered.");
            }
            else
            {
                Height = 1;
                PreviousHash = null;//string.Empty;
            }
            Version = LyraGlobal.DatabaseVersion; // to do: change to global constant; should be used to fork the network; should be validated by comparing with the Node Version (taken from teh same globla contstant)
            BlockType = GetBlockType();

            Sign(PrivateKey, AccountId);

            //File.AppendAllText(@"c:\tmp\hash.txt", $"Sign Block {Hash} New txt: {GetHashInput()}\n");
        }

        public async Task InitializeBlockAsync(Block prevBlock, Func<string, Task<string>> signr)
        {
            if (prevBlock != null)
            {
                Height = prevBlock.Height + 1;
                PreviousHash = prevBlock.Hash;

                if (prevBlock.Hash != prevBlock.CalculateHash())
                    throw new Exception("Invalid previous block, possible data tampered.");
            }
            else
            {
                Height = 1;
                PreviousHash = null;//string.Empty;
            }
            Version = LyraGlobal.DatabaseVersion; // to do: change to global constant; should be used to fork the network; should be validated by comparing with the Node Version (taken from teh same globla contstant)
            BlockType = GetBlockType();

            if (string.IsNullOrWhiteSpace(Hash))
                Hash = CalculateHash();
            Signature = await signr(Hash);
            //File.AppendAllText(@"c:\tmp\hash.txt", $"Sign Block {Hash} New txt: {GetHashInput()}\n");
        }

        public virtual bool AuthCompare(Block other)
        {
            if (other == null)
                return false;

            return Height == other.Height &&
                Version == other.Version &&
                BlockType == other.BlockType &&
                ServiceHash == other.ServiceHash &&
                PreviousHash == other.PreviousHash &&
                JsonConvert.SerializeObject(Tags) == JsonConvert.SerializeObject(other.Tags);
        }

        protected bool CompareDict<T>(Dictionary<string, T> self, Dictionary<string, T> other)
        {
            if (self == null && other == null)
                return true;

            if ((self == null && other != null) || (self != null && other == null))
                return false;

            if (self.Count != other.Count)
                return false;

            foreach (var kvp in self)
            {
                if (!other.ContainsKey(kvp.Key))
                    return false;

                if (!EqualityComparer<T>.Default.Equals(other[kvp.Key], kvp.Value))
                    return false;
            }

            return true;
        }

        public override string GetHashInput()
        {
            return Height.ToString() + "|" +
                             DateTimeToString(TimeStamp) + "|" +
                             this.Version + "|" +
                             this.BlockType.ToString() + "|" +
                             this.ServiceHash + "|" +
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

                if (prevBlock.Height + 1 != this.Height)
                    return false;

                if (prevBlock.Hash != this.PreviousHash)
                    return false;
            }
            else
            {
                if (this.Height != 1) // always 1 for open block
                    return false;
            }

            if (!ValidateTags())
                return false;

            if(string.IsNullOrWhiteSpace(ServiceHash))
            {
                if (BlockType != BlockTypes.Service)
                    return false;

                if (BlockType == BlockTypes.Service && Height > 1)
                    return false;
            }

            if (prevBlock != null && prevBlock.Hash != prevBlock.CalculateHash())
            {                
                return false;
            }                

            return true;
        }

        protected const int MAX_TAGS_COUNT = 16;

        protected const int MAX_STRING_LENGTH = 1024;

        protected virtual bool ValidateTags()
        {
            if (Tags == null)
                return true;

            if (Tags.Count > MAX_TAGS_COUNT)
                throw new Exception("Too many tags");

            foreach (var tag in Tags)
            {
                if (!string.IsNullOrEmpty(tag.Value) && tag.Value.Length > MAX_STRING_LENGTH)
                    throw new Exception("Tag value is too long");

                if (!string.IsNullOrEmpty(tag.Key) && tag.Key.Length > MAX_STRING_LENGTH)
                    throw new Exception("Tag key is too long");
            }

            return true;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"BlockType: {BlockType}\n";
            result += $"Version: {Version}\n";
            result += $"Height: {Height}\n";
            result += $"TimeStamp: {DateTimeToString(TimeStamp)}\n"; 
            result += $"PreviousHash: {PreviousHash}\n";
            result += $"ServiceHash: {ServiceHash}\n";
            result += $"Tags: {JsonConvert.SerializeObject(Tags)}\n";
            //result += $"Authorizations: {JsonConvert.SerializeObject(Authorizations)}\n";
            return result;
        }

        public Block Clone()
        {
            return MemberwiseClone() as Block;
        }

        public void AddTag(string tagKey, string tagValue)
        {
            if (Tags == null)
                Tags = new Dictionary<string, string>();

            if(Tags.ContainsKey(tagKey))
                Tags.Remove(tagKey);

            Tags.Add(tagKey, tagValue);
        }

        public bool ContainsTag(string tagKey)
        {
            return true == Tags?.ContainsKey(tagKey);
        }

        private bool CompareDict(Dictionary<string, long> thisDict, Dictionary<string, long> otherDict)
        {
            if (thisDict == null && otherDict == null)
                return true;

            if (thisDict.Count != otherDict.Count)
                return false;

            foreach (var kvp in thisDict)
            {
                if (!otherDict.ContainsKey(kvp.Key))
                    return false;

                if (otherDict[kvp.Key] != kvp.Value)
                    return false;
            }

            return true;
        }
    }

    public enum BlockTypes : byte
    {
        Null = 0,

        //NullTransaction = 1,

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

        ReceiveAsFee = 24,

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

        ReceiveAuthorizerFee = 36,
        ReceiveNodeProfit = 37,

        // Trading blocks
        // Put Sell or Buy trade order to exchange tokens
        TradeOrder = 40,

        // Send tokens to the trade order to initiate trade
        Trade = 41,

        // Exchange tokens with Trade initiator to conclude the trade and execute the trade order
        ExecuteTradeOrder = 42,

        // Cancels the order and frees up the locked funds
        CancelTradeOrder = 43,

        // Liquidate Pool
        PoolFactory = 50,
        PoolGenesis = 51,
        PoolDeposit = 52,
        PoolWithdraw = 53,
        PoolSwapIn = 54,
        PoolSwapOut = 55,

        // staking
        ProfitingGenesis = 60,
        Profiting = 61,
        Benefiting = 62,

        StakingGenesis = 65,
        Staking = 66,
        UnStaking = 67,

        // DEX
        DexWalletGenesis = 70,
        DexTokenMint = 71,
        DexTokenBurn = 72,
        DexSendToken = 73,
        DexRecvToken = 74,
        DexWithdrawToken = 75,
        
        // DAO
        OrgnizationRecv = 80,
        OrgnizationGenesis = 81,
        OrgnizationSend = 82,
        OrgnizationChange = 83,

        // OTC
        OTCOrderRecv = 84,
        OTCOrderGenesis = 85,
        OTCOrderSend = 86,
        OTCTradeRecv = 87,
        OTCTradeGenesis = 88,
        OTCTradeSend = 89,
        OTCTradeResolutionRecv = 90,

        // voting
        VoteGenesis = 100,
        Voting = 101,
    }

    public enum APIResultCodes
    {
        Success = 0,
        UnknownError = 1,
        // default error code
        UndefinedError = 1000,
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

        InvalidNFT = 6200,
        InvalidCollectibleNFT = 6201,
        DuplicateNFTCollectibleSerialNumber = 6202,
        NFTCollectibleSerialNumberDoesNotExist = 6203,
        InvalidCollectibleNFTDenomination = 6204,
        InvalidCollectibleNFTSerialNumber = 6205,
        NFTInstanceNotFound = 6206,
        NFTSignaturesDontMatch = 6207,

        ТlockHashDoesNotMatch = 59,

        CancelTradeOrderValidationFailed = 63,

        InvalidFeeType = 64,

        InvalidParameterFormat = 65,

        APISignatureValidationFailed = 66,

        InvalidNetworkId = 67,
        CannotSendToSelf = 68,
        InvalidAmountToSend = 69,

        InvalidPreviousBlock,

        CannotModifyImportedAccount,
        AccountAlreadyImported,
        CannotImportEmptyAccount,
        CannotImportAccountWithOtherImports,
        ImportTransactionValidationFailed,
        CannotImportAccountToItself,

        // service blocks related
        InvalidConsolidationMerkleTreeHash = 80,
        InvalidConsolidationTotalFees,
        InvalidConsolidationMissingBlocks,
        InvalidServiceBlockTotalFees,
        InvalidFeeTicker,
        InvalidAuthorizerCount,
        InvalidAuthorizerInServiceBlock,
        InvalidLeaderInServiceBlock,
        InvalidLeaderInConsolidationBlock,
        InvalidConsolidationBlockContinuty,
        InvalidConsolidationBlockCount,
        InvalidConsolidationBlockHashes,

        InvalidSyncFeeBlock,
        


        DuplicateReceiveBlock = 100,

        InvalidTokenRenewalDate = 200,

        TokenExpired = 201,

        NameUnavailable = 202,
        DomainNameTooShort,
        EmptyDomainName,
        DomainNameReserved,

        NotAllowedToSign = 300,
        NotAllowedToCommit = 301,
        BlockFailedToBeAuthorized = 302,
        NodeOutOfSync = 303,
        PBFTNetworkNotReadyForConsensus = 304,
        DoubleSpentDetected = 305,
        NotListedAsQualifiedAuthorizer = 306,
        ConsensusTimeout = 307,
        SystemNotReadyToServe,
        InvalidBlockTimeStamp,

        FailedToSyncAccount,
        APIRouteFailed,
        InvalidDomainName,
        InvalidTickerName,
        InvalidAuthorizerSignatureInServiceBlock,

        InvalidTokenPair = 330,
        PoolAlreadyExists,
        PoolNotExists,
        PoolShareNotExists,
        InvalidPoolOperation,
        PoolOperationAlreadyCompleted,
        InvalidPoolDepositionAmount,
        InvalidPoolDepositionRito,
        InvalidPoolWithdrawAccountId,
        InvalidPoolWithdrawAmount,
        InvalidPoolWithdrawRito, 
        InvalidTokenToSwap,
        TooManyTokensToSwap,
        InvalidPoolSwapOutToken,
        InvalidPoolSwapOutAmount,
        InvalidPoolSwapOutShare,
        InvalidPoolSwapOutAccountId,
        PoolSwapRitoChanged,
        InvalidSwapSlippage,
        SwapSlippageExcceeded,
        PoolOutOfLiquidaty,
        ReQuotaNeeded,  // pool or target account is busy
        InvalidBlockTags,
        InvalidProfitingAccount,
        VotingDaysTooSmall,
        InvalidShareOfProfit,
        DuplicateName,
        InvalidStakingAccount,
        SystemBusy,
        InvalidName,
        InvalidRelatedTx,
        InvalidTimeRange,
        InvalidShareRitio,
        InvalidSeatsCount,
        InvalidMessengerAccount,
        RequestNotPermited,
        DuplicateAccountType,
        InvalidManagementBlock,
        InvalidBrokerAcount,
        InvalidUnstaking,
        InvalidBalance,
        InvalidOpeningAccount,
        InvalidBlockSequence,
        InvalidManagedTransaction,
        ProfitUnavaliable,
        BlockCompareFailed,
        InvalidAmount,

        InvalidBlockData = 400,
        AccountLockDown,
        UnsupportedBlockType,

        UnsuppportedServiceRequest = 500,
        InvalidServiceRequest = 501,
        Unsupported,

        InvalidExternalToken,
        InvalidTokenMint,
        InvalidTokenBurn,
        InvalidWithdrawToAddress,
        InvalidAccountType,
        UnsupportedDexToken,
        InvalidDexServer,
        InvalidExternalAddress,
        TokenNotFound,

        InvalidOrgnization,
        InvalidOrder,
        InvalidTrade,
        NotOwnerOfTrade,
        NotSellerOfTrade,
        InvalidTradeStatus,
        InvalidOrderStatus,
        InvalidCollateral,
        InputTooLong,
        Exception,
        StorageAPIFailure,

        DealerRoomNotExists,
        NotFound,
        InvalidTagParameters,
        InputTooShort,
        CollateralNotEnough,

        InvalidVote,
        InvalidArgument,
        Unauthorized,
        InvalidDAO,
        NotEnoughVoters,
        InvalidDataType,
        NotImplemented,
        InvalidOperation,
        AlreadyExecuted,
        InvalidToken,
        ResourceIsBusy,
        TradesPending,
        ArgumentOutOfRange,
    }
}
