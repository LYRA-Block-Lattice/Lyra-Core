using System;
using System.Collections.Generic;
using TcpHelperLib;
using Newtonsoft.Json;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;

using Lyra.Core.Accounts.Node;

namespace Lyra.Node
{
    class RPCServer : IDisposable
    {
        const int PORT = 11511;


        const string JSON_CONFIG_FILE = "tcpHelperSettings.json";

        //const string TIMER_NAME = "Timer";
        //const string START_STREAMING = "Start Streaming";

        MethodCaller _caller = new MethodCaller();

        List<string> _methods = new List<string>();

        TcpHelper _server;

        RPCMethods _rpc;

        public void Initialize(ServiceAccount serviceAccount, IAccountCollection accountCollection, TradeMatchEngine tradeMatchEngine)
        {
            _rpc = new RPCMethods(serviceAccount, accountCollection, tradeMatchEngine);

            _caller[RPCResult.PROC_GET_SYNC_HEIGHT] =
                 rpi =>
                {
                    try
                    {
                        ProcessingResult pr = new ProcessingResult
                        {
                            StringToSendBack = new RPCResult
                            {
                                ProcedureName = RPCResult.PROC_GET_SYNC_HEIGHT,
                                CallId = rpi.CallId,
                                //CallResult = JsonConvert.SerializeObject(await _rpc.GetSyncHeightAsync())
                                CallResult = JsonConvert.SerializeObject(_rpc.GetSyncHeight().Result)
                            }.ToJson()
                        };
                        return pr;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception in PROC_GET_SYNC_HEIGHT: " + e.Message);
                        return null;
                    }
                };
            _methods.Add(RPCResult.PROC_GET_SYNC_HEIGHT);

            _caller[RPCResult.PROC_GET_TOKEN_NAMES] =
     rpi =>
     {
         try
         {
             var p = rpi.Params;
             var accountid = p[0] as string;
             var signature = p[1] as string;
             var keyword = p[2] as string;

             ProcessingResult pr = new ProcessingResult
             {
                 StringToSendBack = new RPCResult
                 {
                     ProcedureName = RPCResult.PROC_GET_TOKEN_NAMES,

                     CallId = rpi.CallId,
                     //CallResult = JsonConvert.SerializeObject(await _rpc.GetSyncHeightAsync())
                     CallResult = JsonConvert.SerializeObject(_rpc.GetTokenNames(accountid, signature, keyword).Result)
                 }.ToJson()
             };
             return pr;
         }
         catch (Exception e)
         {
             Console.WriteLine("Exception in PROC_GET_TOKEN_NAMES: " + e.Message);
             return null;
         }
     };
            _methods.Add(RPCResult.PROC_GET_TOKEN_NAMES);


            _caller[RPCResult.PROC_GET_ACCOUNT_HEIGHT] =
                 rpi =>
                {
                    try
                    {
                        var p = rpi.Params;
                        var accountid = p[0] as string;
                        var signature = p[1] as string;
                        return new ProcessingResult
                        {
                            StringToSendBack = new RPCResult
                            {
                                ProcedureName = RPCResult.PROC_GET_ACCOUNT_HEIGHT,
                                CallId = rpi.CallId,
                                CallResult = JsonConvert.SerializeObject(_rpc.GetAccountHeight(accountid, signature).Result)
                            }.ToJson()
                        };
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Exception in PROC_GET_ACCOUNT_HEIGHT: " + e.Message);
                        return null;
                    }
                };
            _methods.Add(RPCResult.PROC_GET_ACCOUNT_HEIGHT);


            _caller[RPCResult.PROC_GET_BLOCK_BY_INDEX] =
                 rpi =>
                {
                    var p = rpi.Params;
                    string accountid = p[0] as string;
                    int index = int.Parse(p[1] as string);
                    var signature = p[2] as string;
                    var result = _rpc.GetBlockByIndex(accountid, index, signature);
                    return new ProcessingResult
                    {
                        StringToSendBack = new RPCResult
                        {
                            ProcedureName = RPCResult.PROC_GET_BLOCK_BY_INDEX,
                            CallId = rpi.CallId,
                            CallResult = JsonConvert.SerializeObject(result.Result)
                        }.ToJson()
                    };
                };
            _methods.Add(RPCResult.PROC_GET_BLOCK_BY_INDEX);

            _caller[RPCResult.PROC_GET_BLOCK_BY_HASH] =
                 rpi =>
                 {
                     var p = rpi.Params;
                     string accountid = p[0] as string;
                     string hash = p[1] as string;
                     var signature = p[2] as string;
                     var result = _rpc.GetBlockByHash(accountid, hash, signature);
                     return new ProcessingResult
                     {
                         StringToSendBack = new RPCResult
                         {
                             ProcedureName = RPCResult.PROC_GET_BLOCK_BY_HASH,
                             CallId = rpi.CallId,
                             CallResult = JsonConvert.SerializeObject(result.Result)
                         }.ToJson()
                     };
                 };
            _methods.Add(RPCResult.PROC_GET_BLOCK_BY_HASH);

            _caller[RPCResult.PROC_GET_NONFUNGIBLE_TOKENS] =
             rpi =>
             {
                 try
                 {
                     var p = rpi.Params;
                     var accountid = p[0] as string;
                     var signature = p[1] as string;
                     return new ProcessingResult
                     {
                         StringToSendBack = new RPCResult
                         {
                             ProcedureName = RPCResult.PROC_GET_NONFUNGIBLE_TOKENS,
                             CallId = rpi.CallId,
                             CallResult = JsonConvert.SerializeObject(_rpc.GetNonFungibleTokens(accountid, signature).Result)
                         }.ToJson()
                     };
                 }
                 catch (Exception e)
                 {
                     Console.WriteLine("Exception in PROC_GET_NONFUNGIBLE_TOKENS: " + e.Message);
                     return null;
                 }
             };
            _methods.Add(RPCResult.PROC_GET_NONFUNGIBLE_TOKENS);


            _caller[RPCResult.PROC_GET_TOKEN_GENESIS_BLOCK] =
                 rpi =>
                {
                    var p = rpi.Params;
                    var account_id = p[0] as string;
                    string ticker = p[1] as string;
                    var signature = p[2] as string;
                    var result = _rpc.GetTokenGenesisBlock(account_id, ticker, signature);
                    return new ProcessingResult
                    {
                        StringToSendBack = new RPCResult
                        {
                            ProcedureName = RPCResult.PROC_GET_TOKEN_GENESIS_BLOCK,
                            CallId = rpi.CallId,
                            //ResultType = block != null?block.GetBlockType().ToString():BlockTypes.Null.ToString(),
                            CallResult = JsonConvert.SerializeObject(result.Result)
                        }.ToJson()
                    };
                };
            _methods.Add(RPCResult.PROC_GET_TOKEN_GENESIS_BLOCK);


            _caller[RPCResult.PROC_GET_LAST_SERVICE_BLOCK] =
                rpi =>
               {
                   var p = rpi.Params;
                   var account_id = p[0] as string;
                   var signature = p[1] as string;
                   var result = _rpc.GetLastServiceBlock(account_id, signature);
                   return new ProcessingResult
                   {
                       StringToSendBack = new RPCResult
                       {
                           ProcedureName = RPCResult.PROC_GET_LAST_SERVICE_BLOCK,
                           CallId = rpi.CallId,
                           //ResultType = block != null ? block.GetBlockType().ToString() : BlockTypes.Null.ToString(),
                           CallResult = JsonConvert.SerializeObject(result.Result)
                       }.ToJson()
                   };
               };
            _methods.Add(RPCResult.PROC_GET_LAST_SERVICE_BLOCK);



            _caller[RPCResult.PROC_OPEN_ACCOUNT_WITH_GENESIS] =
                rpi =>
               {
                   var p = rpi.Params;
                   var block = JsonConvert.DeserializeObject<LyraTokenGenesisBlock>(p[0] as string);
                   return new ProcessingResult
                   {
                       StringToSendBack = new RPCResult
                       {
                           ProcedureName = RPCResult.PROC_OPEN_ACCOUNT_WITH_GENESIS,
                           CallId = rpi.CallId,
                           CallResult = JsonConvert.SerializeObject(_rpc.OpenAccountWithGenesis(block).Result)
                       }.ToJson()
                   };
               };
            _methods.Add(RPCResult.PROC_OPEN_ACCOUNT_WITH_GENESIS);


            _caller[RPCResult.PROC_CREATE_TOKEN] =
                rpi =>
               {
                   var p = rpi.Params;
                   var block = JsonConvert.DeserializeObject<TokenGenesisBlock>(p[0] as string);
                   return new ProcessingResult
                   {
                       StringToSendBack = new RPCResult
                       {
                           ProcedureName = RPCResult.PROC_CREATE_TOKEN,
                           CallId = rpi.CallId,
                           CallResult = JsonConvert.SerializeObject(_rpc.CreateToken(block).Result)
                       }.ToJson()
                   };
               };
            _methods.Add(RPCResult.PROC_CREATE_TOKEN);


            _caller[RPCResult.PROC_SEND_TRANSFER] =
                rpi =>
               {
                   var p = rpi.Params;
                   SendTransferBlock block = JsonConvert.DeserializeObject<SendTransferBlock>(p[0] as string);
                   return new ProcessingResult
                   {
                       StringToSendBack = new RPCResult
                       {
                           ProcedureName = RPCResult.PROC_SEND_TRANSFER,
                           CallId = rpi.CallId,
                           CallResult = JsonConvert.SerializeObject(_rpc.SendTransfer(block).Result)
                       }.ToJson()
                   };
               };
            _methods.Add(RPCResult.PROC_SEND_TRANSFER);


            _caller[RPCResult.PROC_RECEIVE_TRANSFER_AND_OPEN_ACCOUNT] =
               rpi =>
              {
                  var p = rpi.Params;
                  var block = JsonConvert.DeserializeObject<OpenWithReceiveTransferBlock>(p[0] as string);
                  return new ProcessingResult
                  {
                      StringToSendBack = new RPCResult
                      {
                          ProcedureName = RPCResult.PROC_RECEIVE_TRANSFER_AND_OPEN_ACCOUNT,
                          CallId = rpi.CallId,
                          CallResult = JsonConvert.SerializeObject(_rpc.ReceiveTransferAndOpenAccount(block).Result)
                      }.ToJson()
                  };
              };
            _methods.Add(RPCResult.PROC_RECEIVE_TRANSFER_AND_OPEN_ACCOUNT);

            _caller[RPCResult.PROC_OPEN_ACCOUNT_WITH_IMPORT] =
               rpi =>
               {
                   var p = rpi.Params;
                   var block = JsonConvert.DeserializeObject<OpenAccountWithImportBlock>(p[0] as string);
                   return new ProcessingResult
                   {
                       StringToSendBack = new RPCResult
                       {
                           ProcedureName = RPCResult.PROC_OPEN_ACCOUNT_WITH_IMPORT,
                           CallId = rpi.CallId,
                           CallResult = JsonConvert.SerializeObject(_rpc.OpenAccountWithImport(block).Result)
                       }.ToJson()
                   };
               };
            _methods.Add(RPCResult.PROC_OPEN_ACCOUNT_WITH_IMPORT);

            _caller[RPCResult.PROC_IMPORT_ACCOUNT] =
               rpi =>
               {
                   var p = rpi.Params;
                   var block = JsonConvert.DeserializeObject<OpenAccountWithImportBlock>(p[0] as string);
                   return new ProcessingResult
                   {
                       StringToSendBack = new RPCResult
                       {
                           ProcedureName = RPCResult.PROC_IMPORT_ACCOUNT,
                           CallId = rpi.CallId,
                           CallResult = JsonConvert.SerializeObject(_rpc.ImportAccount(block).Result)
                       }.ToJson()
                   };
               };
            _methods.Add(RPCResult.PROC_IMPORT_ACCOUNT);

            _caller[RPCResult.PROC_RECEIVE_TRANSFER] =
               rpi =>
              {
                  var p = rpi.Params;
                  var block = JsonConvert.DeserializeObject<ReceiveTransferBlock>(p[0] as string);
                  return new ProcessingResult
                  {
                      StringToSendBack = new RPCResult
                      {
                          ProcedureName = RPCResult.PROC_RECEIVE_TRANSFER,
                          CallId = rpi.CallId,
                          CallResult = JsonConvert.SerializeObject(_rpc.ReceiveTransfer(block).Result)
                      }.ToJson()
                  };
              };
            _methods.Add(RPCResult.PROC_RECEIVE_TRANSFER);

            _caller[RPCResult.PROC_LOOK_FOR_NEW_TRANSFERS] =
                 rpi =>
                {
                    var p = rpi.Params;
                    string accountid = p[0] as string;
                    //(SendTransferBlock SendBlock, TransactionBlock PreviousBlock) blocks = _rpc.LookForNewTransfer(accountid);
                    var signature = p[1] as string;
                    RPCResult res = new RPCResult();

                    res.ProcedureName = RPCResult.PROC_LOOK_FOR_NEW_TRANSFERS;
                    res.CallId = rpi.CallId;
                    res.CallResult = JsonConvert.SerializeObject(_rpc.LookForNewTransfer(accountid, signature).Result);
                    return new ProcessingResult
                    {
                        StringToSendBack = res.ToJson()
                    };
                };
            _methods.Add(RPCResult.PROC_LOOK_FOR_NEW_TRANSFERS);

            _caller[RPCResult.PROC_LOOK_FOR_NEW_TRADES] =
                 rpi =>
                 {
                     var p = rpi.Params;
                     string accountid = p[0] as string;
                     string BuyTokenCode = p[1] as string;
                     string SellTokenCode = p[2] as string;
                     var signature = p[3] as string;
                     RPCResult res = new RPCResult();

                     res.ProcedureName = RPCResult.PROC_LOOK_FOR_NEW_TRADES;
                     res.CallId = rpi.CallId;
                     res.CallResult = JsonConvert.SerializeObject(_rpc.LookForNewTrade(accountid, BuyTokenCode, SellTokenCode, signature).Result);
                     return new ProcessingResult
                     {
                         StringToSendBack = res.ToJson()
                     };
                 };
            _methods.Add(RPCResult.PROC_LOOK_FOR_NEW_TRADES);
            

            _caller[RPCResult.PROC_TRADE_ORDER] =
            rpi =>
            {
                var p = rpi.Params;
                var block = JsonConvert.DeserializeObject<TradeOrderBlock>(p[0] as string);
                return new ProcessingResult
                {
                    StringToSendBack = new RPCResult
                    {
                        ProcedureName = RPCResult.PROC_TRADE_ORDER,
                        CallId = rpi.CallId,
                        CallResult = JsonConvert.SerializeObject(_rpc.TradeOrder(block).Result)
                    }.ToJson()
                };
            };
            _methods.Add(RPCResult.PROC_TRADE_ORDER);

            _caller[RPCResult.PROC_TRADE] =
            rpi =>
            {
                var p = rpi.Params;
                var block = JsonConvert.DeserializeObject<TradeBlock>(p[0] as string);
                return new ProcessingResult
                {
                    StringToSendBack = new RPCResult
                    {
                        ProcedureName = RPCResult.PROC_TRADE,
                        CallId = rpi.CallId,
                        CallResult = JsonConvert.SerializeObject(_rpc.Trade(block).Result)
                    }.ToJson()
                };
            };
            _methods.Add(RPCResult.PROC_TRADE);

            _caller[RPCResult.PROC_EXECUTE_TRADE_ORDER] =
            rpi =>
            {
                var p = rpi.Params;
                var block = JsonConvert.DeserializeObject<ExecuteTradeOrderBlock>(p[0] as string);
                return new ProcessingResult
                {
                    StringToSendBack = new RPCResult
                    {
                        ProcedureName = RPCResult.PROC_EXECUTE_TRADE_ORDER,
                        CallId = rpi.CallId,
                        CallResult = JsonConvert.SerializeObject(_rpc.ExecuteTradeOrder(block).Result)
                    }.ToJson()
                };
            };
            _methods.Add(RPCResult.PROC_EXECUTE_TRADE_ORDER);

            _caller[RPCResult.PROC_CANCEL_TRADE_ORDER] =
            rpi =>
            {
                var p = rpi.Params;
                var block = JsonConvert.DeserializeObject<CancelTradeOrderBlock>(p[0] as string);
                return new ProcessingResult
                {
                    StringToSendBack = new RPCResult
                    {
                        ProcedureName = RPCResult.PROC_CANCEL_TRADE_ORDER,
                        CallId = rpi.CallId,
                        CallResult = JsonConvert.SerializeObject(_rpc.CancelTradeOrder(block).Result)
                    }.ToJson()
                };
            };
            _methods.Add(RPCResult.PROC_CANCEL_TRADE_ORDER);

            _caller[RPCResult.PROC_GET_ACTIVE_TRADE_ORDERS] =
            rpi =>
            {
                var p = rpi.Params;
                var account_id = p[0] as string;
                string sell_token = p[1] as string;
                string buy_token = p[2] as string;
                var order_type = (TradeOrderListTypes)Enum.Parse(typeof(TradeOrderListTypes), p[3] as string);
                var signature = p[4] as string;
                var result = _rpc.GetActiveTradeOrders(account_id, sell_token, buy_token, order_type, signature);
                return new ProcessingResult
                {
                    StringToSendBack = new RPCResult
                    {
                        ProcedureName = RPCResult.PROC_GET_ACTIVE_TRADE_ORDERS,
                        CallId = rpi.CallId,
                        CallResult = JsonConvert.SerializeObject(result.Result)
                    }.ToJson()
                };
            };
            _methods.Add(RPCResult.PROC_GET_ACTIVE_TRADE_ORDERS);


            _server = new TcpHelper(id: "LYRA NODE", processMethod: (dt, lst, clientWrapper, stateProprties) =>
            {
                foreach (string methodName in _methods)
                {
                    var rpi = stateProprties.GetRpi(methodName);
                    if (rpi != null)
                    {
                        stateProprties[rpi.Name] = null;
                        return _caller.ExecuteMethod(rpi);
                    }
                }
                return null;
            },
                configFilePath: JSON_CONFIG_FILE);

            _server.Listen(PORT);


        }

        public void Dispose()
        {
            if (_server != null)
                _server.Stop();
        }


    }



}
