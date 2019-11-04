using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Net;

using Newtonsoft.Json;

using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.API;

namespace Lyra.Client.WebAPI
{
    public class WebAPIClient : INodeAPI
    {
        //Timer timer;

        //const int port = 11511;
        //readonly TcpHelper _clientRpc = null;
        //TcpClientWrapper _tcwRpc = null;

        //readonly Wallet _wallet = null;

        const string _MSG_FAILURE_ERROR = @"WebAPIClient: Method {0} failed with error Code: {1}";

        //readonly ConcurrentDictionary<string, RPCResult> _call_results = new ConcurrentDictionary<string, RPCResult>();

        //readonly ConcurrentDictionary<string, AutoResetEvent> _semaphors = new ConcurrentDictionary<string, AutoResetEvent>();

        //int _call_counter;

        //string SetuUpCallID()
        //{
        //    string callId = Guid.NewGuid().ToString();//_call_counter++.ToString();
        //    AutoResetEvent semaphor = new AutoResetEvent(false);
        //    _semaphors.TryAdd(callId, semaphor);

        //    return callId;
        //}

        HttpClient _client;


        public WebAPIClient(string BaseAddress)
        {
            _client = new HttpClient
            {
                //BaseAddress = new Uri("https://localhost:5001/api/")
                BaseAddress = new Uri(BaseAddress),
#if DEBUG
                Timeout = new TimeSpan(0, 30, 0)        // for debug. but 10 sec is too short for real env
#else
                Timeout = new TimeSpan(0, 0, 30)
#endif
            };
        }

#region INodeAPI implementation

        public async Task<AccountHeightAPIResult> GetSyncHeight()
        {
            try
            {
                string responseBody = await _client.GetStringAsync("getsyncheight");
                return JsonConvert.DeserializeObject<AccountHeightAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AccountHeightAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AccountHeightAPIResult> GetAccountHeight(string AccountId, string Signature)
        {
            try
            {
                string responseBody = await _client.GetStringAsync("getaccountheight/" + AccountId + "/" + Signature);
                return JsonConvert.DeserializeObject<AccountHeightAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AccountHeightAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<BlockAPIResult> GetBlockByIndex(string AccountId, int Index, string Signature)
        {
            try
            {
                string responseBody = await _client.GetStringAsync("getblockbyindex/" + AccountId + "/" + Index.ToString() + "/" + Signature);
                return JsonConvert.DeserializeObject<BlockAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new BlockAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<BlockAPIResult> GetBlockByHash(string AccountId, string Hash, string Signature)
        {
            try
            {
                string responseBody = await _client.GetStringAsync("getblockbyhash/" + AccountId + "/" + Hash + "/" + Signature);
                return JsonConvert.DeserializeObject<BlockAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new BlockAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<NonFungibleListAPIResult> GetNonFungibleTokens(string AccountId, string Signature)
        {
            try
            {
                string responseBody = await _client.GetStringAsync("getnonfungibletokens/" + AccountId + "/" + Signature);
                return JsonConvert.DeserializeObject<NonFungibleListAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new NonFungibleListAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<BlockAPIResult> GetLastServiceBlock(string AccountId, string Signature)
        {
            try
            {
                string responseBody = await _client.GetStringAsync("getlastserviceblock/" + AccountId + "/" + Signature);
                return JsonConvert.DeserializeObject<BlockAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new BlockAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<BlockAPIResult> GetTokenGenesisBlock(string AccountId, string TokenTicker, string Signature)
        {
            try
            {
                string responseBody = await _client.GetStringAsync("gettokengenesisblock/" + AccountId + "/" + TokenTicker + "/" + Signature);
                return JsonConvert.DeserializeObject<BlockAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new BlockAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<ActiveTradeOrdersAPIResult> GetActiveTradeOrders(string AccountId, string SellTokenCode, string BuyTokenCode, TradeOrderListTypes OrderType, string Signature)
        {
            try
            {
                //if (string.IsNullOrEmpty(SellTokenCode) || string.IsNullOrEmpty(BuyTokenCode))
                //    return new ActiveTradeOrdersAPIResult() { ResultCode = APIResultCodes.InvalidParameterFormat, ResultMessage = "Parameter cannot be null; use * to specify ALL values." };

                // we need it to make sure there is something in between slash in the URL.
                // TO DO - Find a way to avoid it in the future
                if (string.IsNullOrEmpty(SellTokenCode))
                    SellTokenCode = "*";

                if (string.IsNullOrEmpty(BuyTokenCode))
                    BuyTokenCode = "*";

                string responseBody = await _client.GetStringAsync("getactivetradeorders/" + AccountId + "/" + SellTokenCode + "/" + BuyTokenCode + "/" + OrderType.ToString() + "/" + Signature);
                return JsonConvert.DeserializeObject<ActiveTradeOrdersAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new ActiveTradeOrdersAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<NewTransferAPIResult> LookForNewTransfer(string AccountId, string Signature)
        {
            try
            {
                string responseBody = await _client.GetStringAsync("lookfornewtransfer/" + AccountId + "/" + Signature);
                return JsonConvert.DeserializeObject<NewTransferAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new NewTransferAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<TradeAPIResult> LookForNewTrade(string AccountId, string BuyTokenCode, string SellTokenCode, string Signature)
        {
            try
            {
                //if (string.IsNullOrEmpty(SellTokenCode) || string.IsNullOrEmpty(BuyTokenCode))
                //    return new TradeAPIResult() { ResultCode = APIResultCodes.InvalidParameterFormat, ResultMessage = "Parameter cannot be null; use * to specify ALL values." };

                // we need it to make sure there is something in between slash in the URL.
                // TO DO - Find a way to avoid it in the future
                if (string.IsNullOrEmpty(SellTokenCode))
                    SellTokenCode = "*";

                if (string.IsNullOrEmpty(BuyTokenCode))
                    BuyTokenCode = "*";


                string responseBody = await _client.GetStringAsync("lookfornewtrade/" + AccountId + "/" + BuyTokenCode + "/" + SellTokenCode + "/" + Signature);
                return JsonConvert.DeserializeObject<TradeAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new TradeAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

#region Authorization methods 

        public async Task<AuthorizationAPIResult> SendTransfer(SendTransferBlock SendBlock)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(SendBlock);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("SendTransfer", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);

                //string serializedblock = JsonConvert.SerializeObject(SendBlock);
                //string responseBody = await _client.GetStringAsync("sendransfer/" + serializedblock);
                //return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> ReceiveTransfer(ReceiveTransferBlock ReceiveBlock)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(ReceiveBlock);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("ReceiveTransfer", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);

                //string serializedblock = JsonConvert.SerializeObject(ReceiveBlock);
                //string responseBody = await _client.GetStringAsync("sendransfer/" + serializedblock);
                //return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> ImportAccount(ImportAccountBlock block)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(block);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("ImportAccount", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> ReceiveTransferAndOpenAccount(OpenWithReceiveTransferBlock OpenReceiveBlock)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(OpenReceiveBlock);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("ReceiveTransferAndOpenAccount", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);

                //string serializedblock = JsonConvert.SerializeObject(OpenReceiveBlock);
                //string responseBody = await _client.GetStringAsync("ReceiveTransferAndOpenAccount/" + serializedblock);
                //return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithImport(OpenAccountWithImportBlock block)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(block);

                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("OpenAccountWithImport", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);

                //string serializedblock = JsonConvert.SerializeObject(OpenReceiveBlock);
                //string responseBody = await _client.GetStringAsync("ReceiveTransferAndOpenAccount/" + serializedblock);
                //return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> OpenAccountWithGenesis(LyraTokenGenesisBlock OpenTokenGenesisBlock)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(OpenTokenGenesisBlock);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("OpenAccountWithGenesis", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);

                //string serializedblock = JsonConvert.SerializeObject(OpenTokenGenesisBlock);
                //string responseBody = await _client.GetStringAsync("OpenAccountWithGenesis/" + serializedblock);
                //return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> CreateToken(TokenGenesisBlock Genesis_Block)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(Genesis_Block);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("CreateToken", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<TradeOrderAuthorizationAPIResult> TradeOrder(TradeOrderBlock block)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(block);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("TradeOrder", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<TradeOrderAuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new TradeOrderAuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> Trade(TradeBlock block)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(block);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("Trade", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> ExecuteTradeOrder(ExecuteTradeOrderBlock block)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(block);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("ExecuteTradeOrder", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

        public async Task<AuthorizationAPIResult> CancelTradeOrder(CancelTradeOrderBlock block)
        {
            try
            {
                var serializedblock = JsonConvert.SerializeObject(block);
                var Content = new StringContent(serializedblock, Encoding.UTF8, "application/json");
                var response = await _client.PostAsync("CancelTradeOrder", Content);
                response.EnsureSuccessStatusCode();
                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<AuthorizationAPIResult>(responseBody);
            }
            catch (HttpRequestException e)
            {
                return new AuthorizationAPIResult() { ResultCode = APIResultCodes.NoRPCServerConnection, ResultMessage = e.Message };
            }
        }

#endregion // Authorization methods

#endregion // INodeAPI implementation

#region private methods


#endregion // private methods




    }



}
