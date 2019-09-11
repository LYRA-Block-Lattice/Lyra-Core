using Newtonsoft.Json;


namespace TcpHelperLib
{
    public class RPCResult
    {
        public const string PROC_GET_SYNC_HEIGHT = "GetSyncHeight";
        public const string PROC_GET_ACCOUNT_HEIGHT = "GetAccountHeight";
        public const string PROC_GET_BLOCK_BY_INDEX = "GetBlockByIndex";
        public const string PROC_GET_BLOCK_BY_HASH = "GetBlockByHash";
        public const string PROC_GET_NONFUNGIBLE_TOKENS = "GetNonFungibleTokens";
        public const string PROC_GET_TOKEN_GENESIS_BLOCK = "GetTokenTokenGenesisBlock";
        public const string PROC_GET_LAST_SERVICE_BLOCK = "GetLastServiceBlock";
        public const string PROC_OPEN_ACCOUNT_WITH_GENESIS = "OpenAccountWithGenesis";
        public const string PROC_SEND_TRANSFER = "SendTransfer";
        public const string PROC_LOOK_FOR_NEW_TRANSFERS = "LookForNewTransfers";
        public const string PROC_RECEIVE_TRANSFER_AND_OPEN_ACCOUNT = "ReceiveTransferAndOpenAccount";
        public const string PROC_RECEIVE_TRANSFER = "ReceiveTransfer";
        public const string PROC_CREATE_TOKEN = "CreateToken";
        public const string PROC_TRADE_ORDER = "TradeOrder";
        public const string PROC_TRADE = "Trade";
        public const string PROC_EXECUTE_TRADE_ORDER = "ExecuteTradeOrder";
        public const string PROC_CANCEL_TRADE_ORDER = "CancelTradeOrder";
        public const string PROC_LOOK_FOR_NEW_TRADES = "LookForNewTrades";
        public const string PROC_GET_ACTIVE_TRADE_ORDERS = "GetActiveTradeOrders";
        public const string PROC_OPEN_ACCOUNT_WITH_IMPORT = "OpenAccountWithImport";
        public const string PROC_IMPORT_ACCOUNT = "ImportAccount";

        public const int CODE_SUCCESS = 0;


        //public const string PROC_SCAN = "Scan";
        //public const string PROC_SYNC = "Sync";

        public string ProcedureName { get; set; }
        public string ResultType { get; set; } 
        public string CallResult { get; set; }
        public string CallId { get; set; }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this);
        }

        public void FromJson(string json)
        {
            RPCResult result = JsonConvert.DeserializeObject<RPCResult>(json);
            ProcedureName = result.ProcedureName;
            CallResult = result.CallResult;
            ResultType = result.ResultType;
            CallId = result.CallId;
        }
    }
     
}
