using System;

namespace Lyra.Core.API
{
    public enum APIResultCodes
    {
        Success = 0,
        UnknownError = -1,
        // default error code
        UndefinedError = -1000,
        BlockWithThisIndexAlreadyExists = -2,
        AccountAlreadyExists = -3,
        AccountDoesNotExist = -4,
        BlockWithThisPreviousHashAlreadyExists = -5, // double-spending attempt - trying to add another block to the same previous block
        BlockValidationFailed = -6,
        TokenGenesisBlockAlreadyExists = -7,
        CouldNotFindLatestBlock = -8,
        NegativeTransactionAmount = -9,
        AccountChainBlockValidationFailed = -10,
        AccountChainSignatureValidationFailed = -11,
        AccountChainBalanceValidationFailed = -12,
        AccountBlockAlreadyExists = -13,
        SourceSendBlockNotFound = -14,
        InvalidDestinationAccountId = -15,
        CouldNotTraceSendBlockChain = -16,
        TransactionAmountDoesNotMatch = -17,
        ExceptionInOpenAccountWithGenesis = -18,
        ExceptionInSendTransfer = -19,
        ExceptionInReceiveTransferAndOpenAccount = -20,
        ExceptionInReceiveTransfer = -21,
        InvalidBlockType = -22,
        ExceptionInCreateToken = -23,
        InvalidFeeAmount = -24,
        InvalidNewAccountBalance = -25,
        SendTransactionValidationFailed = -26,
        ReceiveTransactionValidationFailed = -27,
        TransactionTokenDoesNotMatch = -28,
        BlockSignatureValidationFailed = -29,
        NoNewTransferFound = -30,
        TokenGenesisBlockNotFound = -31,
        ServiceBlockNotFound = -32,
        BlockNotFound = -33,
        NoRPCServerConnection = -34,
        ExceptionInNodeAPI = -35,
        ExceptionInWebAPI = -36,
        PreviousBlockNotFound = -37,
        InsufficientFunds = -38,
        InvalidAccountId = -39,
        InvalidPrivateKey = -40,
        TradeOrderMatchFound = -41,
        InvalidIndexSequence = -42,
        FeatureIsNotSupported = -48,

        // Trade Codes

        ExceptionInTradeOrderAuthorizer = -43,
        ExceptionInTradeAuthorizer = -44,
        ExceptionInExecuteTradeOrderAuthorizer = -45,
        ExceptionInCancelTradeOrderAuthorizer = -46,

        TradeOrderValidationFailed = -47,
        NoTradesFound = -49,
        TradeOrderNotFound = -50,
        InvalidTradeAmount = -51,

        // Non-fungible token codes
        InvalidNonFungibleAmount = -52,
        InvalidNonFungibleTokenCode = -53,
        MissingNonFungibleToken = -54,
        InvalidNonFungibleSenderAccountId = -55,
        NoNonFungibleTokensFound = -56,
        OriginNonFungibleBlockNotFound = -57,
        SourceNonFungibleBlockNotFound = -58,
        OriginNonFungibleBlockHashDoesNotMatch = -59,
        SourceNonFungibleBlockHashDoesNotMatch = -60,
        NonFungibleSignatureVerificationFailed = -61,
        InvalidNonFungiblePublicKey = -62,

        CancelTradeOrderValidationFailed = -63,

        InvalidFeeType = -64,

        InvalidParameterFormat = -65,

        APISignatureValidationFailed = -66,

        InvalidNetworkId = -67,

        DuplicateReceiveBlock = -100,

        InvalidTokenRenewalDate = - 200,

        TokenExpired = -201,

    }
}
