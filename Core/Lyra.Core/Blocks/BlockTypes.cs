namespace Lyra.Core.Blocks
{
    public enum BlockTypes : ushort
    {
        Null = 0,

        // Network service blocks

        ServiceGenesis = 10,

        Service = 11,
               
        Sync = 12,

        // Opening blocks

        // This is the very first block that creates Lyra Gas token on primary shard
        LyraTokenGenesis = 20,

        // account opening block where the first transaction is receive transfer
        OpenAccountWithReceiveTransfer =21,

        // the same as OpenWithReceiveTransfer Block but tells the authorizer that it received fee instead of regular transfer
        OpenAccountWithReceiveFee = 22,

        // Open a new account and import another account
        OpenAccountWithImport = 23,

        // Transaction blocks

        TokenGenesis = 30,

        SendTransfer = 31,
    
        ReceiveTransfer = 32,
        
        // adds tarnsfers' fee to authorizer's account; 
        // the fee is settled when a new sync or service block is generated, for the previous service Index, 
        // by summarizing all the fee amounts from all blocks with the same corresponding sefrviceblock hash and dividing it by the number of authorizers in the sample;
        // the block can be validated by the next sample and all other nores in the same way;
        // fee data is not encrypted 
        ReceiveFee = 33,

        // Imports an account into current account
        ImportAccount = 34,

        // Trading blocks

        // Put Sell or Buy trade order to exchange tokens
        TradeOrder = 40,

        // Send tokens to the trade order to initiate trade
        Trade = 41,

        // Exchange tokens with Trade initiator to conclude the trade and execute the trade order
        ExecuteTradeOrder = 42,

        // Cancels the order and frees up the locked funds
        CancelTradeOrder = 43,
    }
}
