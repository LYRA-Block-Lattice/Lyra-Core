using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using TcpHelperLib;

using Newtonsoft.Json;

using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.API;

namespace Lyra.Client.RPC
{
    public class RPCClient : IDisposable, INodeAPI
    {
        //Timer timer;

        const int port = 11511;
        readonly TcpHelper _clientRpc = null;
        TcpClientWrapper _tcwRpc = null;

        //readonly Wallet _wallet = null;

        const string _MSG_FAILURE_ERROR = @"RPCClient: Method {0} failed with error Code: {1}";

        readonly ConcurrentDictionary<string, RPCResult> _call_results = new ConcurrentDictionary<string, RPCResult>();

        readonly ConcurrentDictionary<string, AutoResetEvent> _semaphors = new ConcurrentDictionary<string, AutoResetEvent>();

        //int _call_counter;

        string SetuUpCallID()
        {
            string callId = Guid.NewGuid().ToString();//_call_counter++.ToString();
            AutoResetEvent semaphor = new AutoResetEvent(false);
            _semaphors.TryAdd(callId, semaphor);

            return callId;
        }

        //public RPCClient(Wallet wallet)
        public RPCClient(string AccountId)
        {
            //_wallet = wallet;
            //_clientRpc = new TcpHelper(id: _wallet.AccountId, 
            _clientRpc = new TcpHelper(id: AccountId,
                processMethod: (timestamp, lstByte, clientWrapper, dctState) =>
                {
                    try
                    {
                        ResponseHandler("RPC", timestamp, _clientRpc, clientWrapper, lstByte);
                        return null;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception in processMethod: " + e.Message);
                        return null;
                    }
                }
            );


            //timer = new Timer( async _ =>
            //{
            //    try
            //    {
            //        if (_clientRpc != null && (_tcwRpc == null || !_tcwRpc.IsConnected))
            //        {
            //            Console.WriteLine("Connecting to RPC Server");
            //            _tcwRpc = await _clientRpc.Connect(port);
            //        }
            //    }
            //    catch (Exception e)
            //    {
            //        Console.WriteLine("Exception in Connect() timer: " + e.Message);
            //    }
            //},
            //null, 0, 30000);
        }

        public bool IsConnected
        {
            get
            {
                lock (this)
                {
                    Connect();
                    return (_tcwRpc != null && _tcwRpc.IsConnected);
                }
            }
        }

        //public async Task<bool> Connected()
        //{
        //    { 
        //        await Connect();
        //        Connect();
        //        return (_tcwRpc != null && _tcwRpc.IsConnected);
        //    }
        //}

        void Connect()
        {
            try
            {
                if (_tcwRpc == null || !_tcwRpc.IsConnected)
                {
                    Console.WriteLine("Connecting to RPC Server at port " + port.ToString());
                    _tcwRpc = _clientRpc.Connect(port).Result;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception in Connect(): " + e.Message);
            }

        }


        //public void GetSyncHeight()
        //{
        //    ProcessingResult request = new ProcessingResult();

        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_SYNC_HEIGHT, null).ToBytes();
        //    Send(request);
        //}

        //public async Task<int> GetSyncHeightAsync()
        //{
        //    string callId = GetCallID();
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_SYNC_HEIGHT, callId).ToBytes();
        //    return await SendAndReceive(callId, request);
        //}


        //public void GetAccountHeight()
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_ACCOUNT_HEIGHT, null, _wallet.AccountId).ToBytes();
        //    Send(request);
        //}

        //public async Task<int> GetAccountHeightAsync()
        //{
        //    string callId = GetCallID();
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_ACCOUNT_HEIGHT, callId, _wallet.AccountId).ToBytes();
        //    return await SendAndReceive(callId, request);
        //}

        #region INodeAPI implementation

        public async Task<AccountHeightAPIResult> GetSyncHeight()
        {
            if (!IsConnected)
                return new AccountHeightAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_SYNC_HEIGHT, callId).ToBytes();
            //return SendAndReceive(callId, request);
            var apiresult = await SendAndReceiveAsync(callId, request);
            if (apiresult == null || apiresult.CallResult == null)
                return new AccountHeightAPIResult() { ResultCode = APIResultCodes.UnknownError };

            var res = JsonConvert.DeserializeObject<AccountHeightAPIResult>(apiresult.CallResult);

            return res;
        }



        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            if (!IsConnected)
                return new AccountHeightAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_ACCOUNT_HEIGHT, callId, AccountId, Signature).ToBytes();

            var apiresult = await SendAndReceiveAsync(callId, request);

            if (apiresult == null || apiresult.CallResult == null)
                return new AccountHeightAPIResult() { ResultCode = APIResultCodes.UnknownError };

            //Console.WriteLine($"API Call Result: {apiresult.CallResult}");

            return JsonConvert.DeserializeObject<AccountHeightAPIResult>(apiresult.CallResult);
        }

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, int Index, string Signature)
        {
            if (!IsConnected)
                return new BlockAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_BLOCK_BY_INDEX, callId, AccountId, Index.ToString(), Signature).ToBytes();
            var apiresult = await SendAndReceiveAsync(callId, request);
            return JsonConvert.DeserializeObject<BlockAPIResult>(apiresult.CallResult);
        }

        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            if (!IsConnected)
                return new BlockAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_BLOCK_BY_HASH, callId, AccountId, Hash, Signature).ToBytes();
            var apiresult = await SendAndReceiveAsync(callId, request);
            return JsonConvert.DeserializeObject<BlockAPIResult>(apiresult.CallResult);
        }

        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            if (!IsConnected)
                return new NonFungibleListAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_NONFUNGIBLE_TOKENS, callId, AccountId, Signature).ToBytes();
            var apiresult = await SendAndReceiveAsync(callId, request);
            return JsonConvert.DeserializeObject<NonFungibleListAPIResult>(apiresult.CallResult);
        }

        public async Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            if (!IsConnected)
                return new BlockAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };


            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_LAST_SERVICE_BLOCK, callId, AccountId, Signature).ToBytes();
            var apiresult = await SendAndReceiveAsync(callId, request);
            return JsonConvert.DeserializeObject<BlockAPIResult>(apiresult.CallResult);
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            if (!IsConnected)
                return new BlockAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_TOKEN_GENESIS_BLOCK, callId, AccountId, TokenTicker, Signature).ToBytes();
            var apiresult = await SendAndReceiveAsync(callId, request);
            //return ParseBlock(apiresult) as TokenGenesisBlock;
            return JsonConvert.DeserializeObject<BlockAPIResult>(apiresult.CallResult);
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellToken, string BuyToken, TradeOrderListTypes OrderType, string Signature)
        {
            if (!IsConnected)
                return new ActiveTradeOrdersAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_ACTIVE_TRADE_ORDERS, callId, AccountId, SellToken, BuyToken, OrderType.ToString(), Signature).ToBytes();
            var apiresult = await SendAndReceiveAsync(callId, request);
            //return ParseBlock(apiresult) as TokenGenesisBlock;
            return JsonConvert.DeserializeObject<ActiveTradeOrdersAPIResult>(apiresult.CallResult);
        }

        //public (SendTransferBlock, TransactionBlock) LookForNewTransfer(string AccountId)
        //{
        //    string callId = SetuUpCallID();
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_LOOK_FOR_NEW_TRANSFERS, callId, AccountId).ToBytes();
        //    var apiresult = SendAndReceive(callId, request);
        //    return ParseTransactionBlock(apiresult);
        //}

        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            if (!IsConnected)
                return new NewTransferAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_LOOK_FOR_NEW_TRANSFERS, callId, AccountId, Signature).ToBytes();
            var apiresult = await SendAndReceiveAsync(callId, request);
            return JsonConvert.DeserializeObject<NewTransferAPIResult>(apiresult.CallResult);
        }

        public async Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            if (!IsConnected)
                return new TradeAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_LOOK_FOR_NEW_TRADES, callId, AccountId, BuyTokenCode, SellTokenCode, Signature).ToBytes();
            var apiresult = await SendAndReceiveAsync(callId, request);
            return JsonConvert.DeserializeObject<TradeAPIResult>(apiresult.CallResult);
        }

        #region Authorization methods 

        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock SendBlock)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_SEND_TRANSFER, callId, JsonConvert.SerializeObject(SendBlock)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock ReceiveBlock)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_RECEIVE_TRANSFER, callId, JsonConvert.SerializeObject(ReceiveBlock)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock OpenReceiveBlock)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_RECEIVE_TRANSFER_AND_OPEN_ACCOUNT, callId, JsonConvert.SerializeObject(OpenReceiveBlock)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_IMPORT_ACCOUNT, callId, JsonConvert.SerializeObject(block)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock OpenTokenGenesisBlock)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack =
                Proxy.ToJson(RPCResult.PROC_OPEN_ACCOUNT_WITH_GENESIS, callId, JsonConvert.SerializeObject(OpenTokenGenesisBlock)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack =
                Proxy.ToJson(RPCResult.PROC_OPEN_ACCOUNT_WITH_IMPORT, callId, JsonConvert.SerializeObject(block)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock Genesis_Block)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_CREATE_TOKEN, callId, JsonConvert.SerializeObject(Genesis_Block)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block)
        {
            if (!IsConnected)
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_TRADE_ORDER, callId, JsonConvert.SerializeObject(block)).ToBytes();

            var apiresult = await SendAndReceiveAsync(callId, request);
            return JsonConvert.DeserializeObject<TradeOrderAuthorizationAPIResult>(apiresult.CallResult);
        }

        public async Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_TRADE, callId, JsonConvert.SerializeObject(block)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_EXECUTE_TRADE_ORDER, callId, JsonConvert.SerializeObject(block)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }

        public async Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            if (!IsConnected)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection };

            string callId = SetuUpCallID();
            ProcessingResult request = new ProcessingResult();
            request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_CANCEL_TRADE_ORDER, callId, JsonConvert.SerializeObject(block)).ToBytes();

            return await SendAndReceiveAuthorizationAsync(callId, request);
        }


        #endregion // Authorization methods

        #endregion // INodeAPI implementation

        #region call result processing
        async Task<RPCResult> GetCallResult(string callId)
        {
            var blockedResultTask = Task.Run(() => WaitForCallResult(callId));

            var result = await blockedResultTask;

            if (result == null)
                throw new ApplicationException("Response timeout");

            return result;
        }

        RPCResult WaitForCallResult(string callId)
        {
#if DEBUG
            _semaphors[callId].WaitOne(10000000);
#else
            _semaphors[callId].WaitOne(10000);
#endif
            _semaphors.TryRemove(callId, out AutoResetEvent semaphore);

            _call_results.TryRemove(callId, out RPCResult result);

            return result;
        }

        //int SendAndReceive(string callId, ProcessingResult request)
        //{
        //    //await SendAsync(request);
        //    //RPCResult result = await GetCallResult(callId);
        //    Send(request);
        //    RPCResult result = GetCallResult(callId).Result;

        //}

        //RPCResult SendAndReceive(string callId, ProcessingResult request)
        //{
        //    Send(request);
        //    return GetCallResult(callId).Result;
        //}

        async Task<RPCResult> SendAndReceiveAsync(string callId, ProcessingResult request)
        {
            await SendAsync(request);
            return await GetCallResult(callId);
        }

        //AuthorizationAPIResult SendAndReceiveAuthorization(string callId, ProcessingResult request)
        //{
        //    var apiresult = SendAndReceive(callId, request);
        //    if (apiresult == null || apiresult.CallResult == null)
        //        return new AuthorizationAPIResult() { ResultCode = APIResultCodes.UnknownError };

        //    return (AuthorizationAPIResult)JsonConvert.DeserializeObject<AuthorizationAPIResult>(apiresult.CallResult);
        //}

        async Task<AuthorizationAPIResult> SendAndReceiveAuthorizationAsync(string callId, ProcessingResult request)
        {
            var apiresult = await SendAndReceiveAsync(callId, request);
            if (apiresult == null || apiresult.CallResult == null)
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.UnknownError };

            return (AuthorizationAPIResult)JsonConvert.DeserializeObject<AuthorizationAPIResult>(apiresult.CallResult);
        }




        //APIResult SendAndReceive(string callId, ProcessingResult request)
        //{
        //    Send(request);
        //    RPCResult rpcresult = GetCallResult(callId).Result;
        //    APIResult apiresult = (APIResult)JsonConvert.DeserializeObject<APIResult>(rpcresult.CallResult);
        //    return apiresult;
        //}

#endregion // call result processing





        //public void OpenAccountWithGenesis(TokenGenesisBlock testTokenGenesisBlock, OpenBlock firstBlock)
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_OPEN_ACCOUNT_WITH_GENESIS, null, JsonConvert.SerializeObject(testTokenGenesisBlock), JsonConvert.SerializeObject(firstBlock)).ToBytes();
        //    Send(request);
        //}

        //public void CreateToken(TokenGenesisBlock tokenBlock)
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_CREATE_TOKEN, null, JsonConvert.SerializeObject(tokenBlock)).ToBytes();
        //    Send(request);
        //}

        //public void SendTransfer(SendTransferBlock sendBlock)
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_SEND_TRANSFER, null, JsonConvert.SerializeObject(sendBlock)).ToBytes();
        //    Send(request);
        //}

        //public void ReceiveTransferAndOpenAccount(OpenBlock openBlock)
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_RECEIVE_TRANSFER_AND_OPEN_ACCOUNT, null, JsonConvert.SerializeObject(openBlock)).ToBytes();
        //    Send(request);
        //}

        //public void ReceiveTransfer(ReceiveTransferBlock receiveBlock)
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_RECEIVE_TRANSFER, null, JsonConvert.SerializeObject(receiveBlock)).ToBytes();
        //    Send(request);
        //}

        //public void GetBlock(int Index)
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_BLOCK, null, _wallet.AccountId, Index.ToString()).ToBytes();
        //    Send(request);
        //}

        //public void GetTokenTokenGenesisBlock(string TokenTicker)
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_TOKEN_GENESIS_BLOCK, null, TokenTicker).ToBytes();
        //    Send(request);
        //}

        //public void GetLastServiceBlock()
        //{
        //    ProcessingResult request = new ProcessingResult();
        //    request.BytesToSendBack = Proxy.ToJson(RPCResult.PROC_GET_LAST_SERVICE_BLOCK, null).ToBytes();
        //    Send(request);
        //}

#region private methods

        //private void Send(ProcessingResult request)
        //{
        //    //if (_clientRpc != null && _tcwRpc != null && _tcwRpc.IsConnected)
        //    //{
        //        //Console.WriteLine(input);
        //        _tcwRpc.SendAsync(request.BytesToSendBack);
        //    //}
        //}

        private async Task SendAsync(ProcessingResult request)
        {
            await _tcwRpc.SendAsync(request.BytesToSendBack);
        }




        //private Block ParseBlock(RPCResult result)
        //{
        //    Block block;
        //    BlockTypes blockType = Enum.Parse<BlockTypes>(result.ResultType);
        //    switch (blockType)
        //    {
        //        case BlockTypes.SendTransfer:
        //            block = JsonConvert.DeserializeObject<SendTransferBlock>(result.CallResult);
        //            break;
        //        case BlockTypes.Genesis:
        //            block = JsonConvert.DeserializeObject<TokenGenesisBlock>(result.CallResult);
        //            break;
        //        case BlockTypes.OpenWithGenesis:
        //            block = JsonConvert.DeserializeObject<OpenWithTokenGenesisBlock>(result.CallResult);
        //            break;
        //        case BlockTypes.ReceiveTransfer:
        //            block = JsonConvert.DeserializeObject<ReceiveTransferBlock>(result.CallResult);
        //            break;
        //        case BlockTypes.OpenWithReceiveTransfer:
        //            block = JsonConvert.DeserializeObject<OpenWithReceiveTransferBlock>(result.CallResult);
        //            break;
        //        case BlockTypes.ReceiveFee:
        //            block = JsonConvert.DeserializeObject<ReceiveFeeBlock>(result.CallResult);
        //            break;
        //        case BlockTypes.OpenWithReceiveFee:
        //            block = JsonConvert.DeserializeObject<OpenWithReceiveFeeBlock>(result.CallResult);
        //            break;
        //        case BlockTypes.Null:
        //            block = null;
        //            break;
        //        default:
        //            throw new ApplicationException("Unknown block type");
        //    }
        //    return block;
        //}

#endregion // private methods

        public void ResponseHandler(string name, DateTime timestamp, TcpHelper clientHelper,
                                  TcpClientWrapper clientWrapper, List<byte> lstByte)
        {

            string response = "no response bytes";
            try
            {
                if (!string.IsNullOrWhiteSpace(lstByte.ToStr()))
                    response = lstByte.ToStr();

                if (response.Contains(TcpHelperLib.TcpHelper.ackConnection_default))
                {
                    //Console.WriteLine(response);
                    //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                    return;
                }

                RPCResult result = new RPCResult();
                result.FromJson(response);
                //Console.WriteLine($"ProcedureName: {result.ProcedureName}, CallResult: {result.CallResult}");

                if (!string.IsNullOrEmpty(result.CallId))
                {
                    if (_semaphors.ContainsKey(result.CallId))
                    {
                        _call_results.TryAdd(result.CallId, result);
                        _semaphors[result.CallId].Set();
                        return;
                    }

                }

                //if (result.ProcedureName == RPCResult.PROC_GET_BLOCK)
                //{
                //    //Block block = ParseBlock(result);
                //    //if (block != null)
                //    //{
                //    //    _wallet.AddBlock(block);

                //    //    //Console.WriteLine($"Added New Block with Index: {block.Index}, Type: {block.GetType().ToString()}");
                //    //    Console.WriteLine("Balance: " + _wallet.DisplayBalances());

                //    //    //Console.WriteLine("New Balance: " + _wallet.DisplayBalances());
                //    //    Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //    //}


                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_GET_TOKEN_GENESIS_BLOCK)
                //{
                //    //TokenGenesisBlock block = ParseBlock(result) as TokenGenesisBlock;

                //    //if (block != null)
                //    //{
                //    //    _wallet.SaveTokenInfoBlock(block);

                //    //    Console.WriteLine($"Found Token Genesis Block for {block.Ticker}");
                //    //    Console.WriteLine("Balance: " + _wallet.DisplayBalances());

                //    //    Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //    //}

                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_GET_SYNC_HEIGHT)
                //{
                //    //int syncheight = (int)JsonConvert.DeserializeObject<int>(result.CallResult);
                //    //if (_wallet.SyncHeight == -1)
                //    //{
                //    //    //Console.WriteLine($"Current Service Chain Height: {syncheight}");
                //    //    //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //    //}

                //    //if (_wallet.SyncHeight != syncheight)
                //    //{
                //    //    _wallet.SyncHeight = syncheight;
                //    //    Console.WriteLine($"Current Service Chain Height: {syncheight}");
                //    //    Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //    //}

                //    //if (_wallet.TransferFee == 0 || _wallet.TokenGenerationFee == 0)
                //    //{
                //    //    GetLastServiceBlock();
                //    //}

                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_GET_LAST_SERVICE_BLOCK)
                //{
                //    //ServiceBlock lastServiceBlock = (ServiceBlock)JsonConvert.DeserializeObject<ServiceBlock>(result.CallResult);
                //    //if (lastServiceBlock != null)
                //    //{
                //    //    _wallet.TransferFee = lastServiceBlock.TransferFee;
                //    //    _wallet.TokenGenerationFee = lastServiceBlock.TokenGenerationFee;
                //    //    Console.WriteLine($"Last Service Block Received {lastServiceBlock.Index}");
                //    //    Console.Write(string.Format("Transfer Fee: {0} ", lastServiceBlock.TransferFee/100));
                //    //    Console.Write(string.Format("Token Generation Fee: {0} ", lastServiceBlock.TokenGenerationFee/100));
                //    //    Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //    //}
                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_GET_ACCOUNT_HEIGHT)
                //{
                //    //int height = (int)JsonConvert.DeserializeObject<int>(result.CallResult);

                //    //int blockCount = _wallet.GetBlockCount();
                //    //if (height > blockCount)
                //    //{
                //    //    Console.WriteLine($"New Account Height Received: {height}");
                //    //    Console.WriteLine($"Requesting next block: {blockCount + 1}");
                //    //    Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //    //    GetBlock(blockCount + 1);
                //    //}
                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_OPEN_ACCOUNT_WITH_GENESIS)
                //{
                //    //int resultcode = (int)JsonConvert.DeserializeObject<int>(result.CallResult);

                //    //if (resultcode != 0)
                //    //{
                //    //    Console.WriteLine($"Failed to add genesis block with error code: {resultcode}");
                //    //}
                //    //else
                //    //{
                //    //    Console.WriteLine($"Genesis block has been authorized successfully");

                //    //}
                //    //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_CREATE_TOKEN)
                //{
                //    //int resultcode = (int)JsonConvert.DeserializeObject<int>(result.CallResult);

                //    //if (resultcode != RPCResult.CODE_SUCCESS)
                //    //{
                //    //    Console.WriteLine(string.Format(_MSG_FAILURE_ERROR, RPCResult.PROC_CREATE_TOKEN, resultcode));
                //    //}
                //    //else
                //    //{
                //    //    Console.WriteLine($"Token generation has been authorized successfully");
                //    //}
                //    //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_SEND_TRANSFER)
                //{
                //    //int resultcode = (int)JsonConvert.DeserializeObject<int>(result.CallResult);

                //    //if (resultcode != 0)
                //    //{
                //    //    Console.WriteLine($"Failed to add send transfer block with error code: {resultcode}");
                //    //}
                //    //else
                //    //{
                //    //    Console.WriteLine($"Send Transfer block has been authorized successfully");

                //    //}
                //    //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //}
                //else
                //    if (result.ProcedureName == RPCResult.PROC_LOOK_FOR_NEW_TRANSFERS)
                //{
                //    //(SendTransferBlock, TransactionBlock) blocks = parseTransactionBlock();

                //    //if (blocks.Item1 != null && blocks.Item2 != null)
                //    //{
                //    //    _wallet.ReceiveTransfer(blocks.Item1, blocks.Item2);

                //    //    Console.WriteLine($"Received new transaction, sending request for settlement...");
                //    //    Console.Write(string.Format("{0}> ", _wallet.AccountName));

                //    //}


                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_RECEIVE_TRANSFER)
                //{
                //    //int resultcode = (int)JsonConvert.DeserializeObject<int>(result.CallResult);

                //    //if (resultcode != 0)
                //    //{
                //    //    Console.WriteLine($"Failed to authorize receive transfer block with error code: {resultcode}");
                //    //}
                //    //else
                //    //{
                //    //    Console.WriteLine($"Receive transfer block has been authorized successfully");
                //    //}
                //    //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //}
                //else
                //if (result.ProcedureName == RPCResult.PROC_RECEIVE_TRANSFER_AND_OPEN_ACCOUNT)
                //{
                //    //int resultcode = (int)JsonConvert.DeserializeObject<int>(result.CallResult);

                //    //if (resultcode != 0)
                //    //{
                //    //    Console.WriteLine($"Failed to authorize receive transfer block with error code: {resultcode}");
                //    //}
                //    //else
                //    //{
                //    //    Console.WriteLine($"Receive transfer block has been authorized successfully");
                //    //}
                //    //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                //}

            }
            catch (Exception e)
            {
                //Console.WriteLine($"{name}: {timestamp} ** {response}");
                //Console.WriteLine($"Exception in ResponseHandler: {e}");
                //Console.Write(string.Format("{0}> ", _wallet.AccountName));
                throw e;
            }
        }

        //private (SendTransferBlock, TransactionBlock) ParseTransactionBlock(RPCResult result)
        //{
        //    (SendTransferBlock, TransactionBlock) theblocks;
        //    BlockTypes transactionBlockType = (BlockTypes)Enum.Parse(typeof(BlockTypes), result.ResultType);
        //    if (transactionBlockType == BlockTypes.Null)
        //        return (null, null);

        //    switch (transactionBlockType)
        //    {
        //        case BlockTypes.Genesis:
        //            theblocks = JsonConvert.DeserializeObject<(SendTransferBlock, TokenGenesisBlock)>(result.CallResult);
        //            break;
        //        case BlockTypes.OpenWithGenesis:
        //            theblocks = JsonConvert.DeserializeObject<(SendTransferBlock, OpenWithTokenGenesisBlock)>(result.CallResult);
        //            break;
        //        case BlockTypes.OpenWithReceiveTransfer:
        //            theblocks = JsonConvert.DeserializeObject<(SendTransferBlock, OpenWithReceiveTransferBlock)>(result.CallResult);
        //            break;
        //        case BlockTypes.SendTransfer:
        //            theblocks = JsonConvert.DeserializeObject<(SendTransferBlock, SendTransferBlock)>(result.CallResult);
        //            break;
        //        case BlockTypes.ReceiveTransfer:
        //            theblocks = JsonConvert.DeserializeObject<(SendTransferBlock, ReceiveTransferBlock)>(result.CallResult);
        //            break;
        //        case BlockTypes.OpenWithReceiveFee:
        //            theblocks = JsonConvert.DeserializeObject<(SendTransferBlock, OpenWithReceiveFeeBlock)>(result.CallResult);
        //            break;
        //        case BlockTypes.ReceiveFee:
        //            theblocks = JsonConvert.DeserializeObject<(SendTransferBlock, ReceiveFeeBlock)>(result.CallResult);
        //            break;
        //        default:
        //            throw new ApplicationException("Unknown block type");
        //    }
        //    return theblocks;
        //}

        public void Dispose()
        {
            if (_clientRpc != null)
                _clientRpc.Stop();
        }

    }



}
