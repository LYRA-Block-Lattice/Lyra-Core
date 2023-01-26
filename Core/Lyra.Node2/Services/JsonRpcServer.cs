using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Noded.Services;
using StreamJsonRpc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lyra.Data.Utils;
using System.Linq;
using Lyra.Data.Blocks;
using Lyra.Data.API.WorkFlow.UniMarket;
using Newtonsoft.Json.Linq;
using static Google.Protobuf.Reflection.FieldOptions.Types;
using Humanizer;
using Newtonsoft.Json;
using System.Reactive;
using Akka.Util;

namespace Lyra.Node
{
    /*
     * Joseph Fowler, [22.02.21 16:41]
        I have a better idea. provides jsonrpc service not in the wallet cli but in the node. 
    first we get better performance, second, we get realtime notify on receiving transfer (by signalR). 
    third, we not clone the old wallet api on nodes but design a new high efficiency one, 
    perhaps 10x speed up. as for the cli, I'll simply add a api to allow reading 
    private key if have password. all wallet should use the new jsonrpc api provided by node. 
    the Lyra Broker project can be abandoned, for exchange, using the  same api is enough.
     */
    public class JsonRpcServer : JsonRpcServerBase
    {
        public JsonRpcServer(INodeAPI node, INodeTransactionAPI trans) : base(node, trans)
        {

        }

        private async Task<KeylessWallet> CreateWalletAsync(string accountId)
        {
            var klWallet = new KeylessWallet(accountId, async (msg) =>
            {
                // [type, signature] = Sign ([ type, message, accountId ])
                var result3 = RPC.InvokeAsync<string[]>("Sign", new object[] { "hash", msg, accountId });
                try
                {
                    var signaturs = await result3;
                    if (signaturs[0] == "p1393")
                        return signaturs[1];
                    else if (signaturs[0] == "der") // der, bouncycastle compatible
                    {
                        var dotnetsignBuff = SignatureHelper.ConvertDerToP1393(signaturs[1].StringToByteArray());
                        var dotnetsign = Base58Encoding.Encode(dotnetsignBuff);
                        return dotnetsign;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch
                {
                    return null;
                }
            }, LyraNodeConfig.GetNetworkId());

            await klWallet.InitAsync();
            
            return klWallet;
        }

        private int CompareVersion(Version v1, Version v2)
        {
            // LYRA Block Lattice 2.1.0.0
            // ommit smallest one
            var s1 = v1.Major * 100 + v1.Minor;
            var s2 = v2.Major * 100 + v2.Minor;
            if (s1 > s2) return 1;
            if (s1 == s2) return 0;
            return -1;
        }

        // group hand shake
        [JsonRpcMethod("Status")]
        public async Task<ApiStatus> StatusAsync(string version, string networkid)
        {
            var clientVer = new Version(version);
            if (CompareVersion(LyraGlobal.NODE_VERSION, clientVer) > 0)
                throw new Exception("Client version too low. Need upgrade.");

            var syncState = await _node.GetSyncStateAsync();

            return new ApiStatus
            {
                version = LyraGlobal.NODE_VERSION.ToString(),
                networkid = syncState.NetworkID,
                synced = syncState.Status.state == Data.API.BlockChainState.Almighty
                    || syncState.Status.state == Data.API.BlockChainState.Engaging
            };
        }

        [JsonRpcMethod("BlockBalance")]
        public async Task<BalanceResult> BlockBalanceAsync(string accountId, TransactionBlock tx)
        {
            if (tx == null)
                return await BalanceAsync(accountId);

            var anySendResult = await _node.LookForNewTransfer2Async(tx.AccountID, null);
            return new BalanceResult
            {
                balance = tx.Balances.ToDecimalDict(),
                unreceived = anySendResult.Successful(),
                height = tx.Height
            };
        }

        // group wallet
        [JsonRpcMethod("Balance")]
        public async Task<BalanceResult> BalanceAsync(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentNullException("accountId can't be null");

            if (!Signatures.ValidateAccountId(accountId))
                throw new Exception("accountId is not a valid Lyra address");

            var blockResult = await _node.GetLastBlockAsync(accountId);
            var anySendResult = await _node.LookForNewTransfer2Async(accountId, null);

            if (blockResult.Successful())
            {
                var block = blockResult.GetBlock() as TransactionBlock;

                return new BalanceResult
                {
                    balance = block.Balances.ToDecimalDict(),
                    unreceived = anySendResult.Successful(),
                    height = block.Height
                };
            }
            else if (blockResult.ResultCode == APIResultCodes.BlockNotFound)
            {
                return new BalanceResult
                {
                    balance = null,
                    unreceived = anySendResult.Successful()
                };
            }

            throw new Exception("Can't get latest block for account.");
        }

        [JsonRpcMethod("Receive")]
        public async Task<BalanceResult> ReceiveAsync(string accountId)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.SyncAsync();
            if (result == APIResultCodes.Success)
            {
                return await BlockBalanceAsync(accountId, klWallet.LastBlock);
            }
            else
            {
                throw new Exception($"{result}");
            }
        }

        [JsonRpcMethod("Send")]
        public async Task<SendResult> SendAsync(string accountId, decimal amount, string destAccount, string ticker)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.SendAsync(amount, destAccount, ticker);
            if (result.Successful())
            {
                var hash = klWallet.LastBlock.Hash;
                var balanceResult = await BlockBalanceAsync(accountId, klWallet.LastBlock);
                return new SendResult
                {
                    txHash = hash,
                    balance = balanceResult.balance,
                    height = balanceResult.height,
                    unreceived = balanceResult.unreceived
                };
            }
            else
            {
                throw new Exception(result.ToString());
            }
        }

        [JsonRpcMethod("Token")]
        public async Task<BalanceResult> TokenAsync(string accountId, string name, string domain, decimal supply)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.CreateTokenAsync(name, domain,
                "", 8, supply, true, "", "", "", ContractTypes.Cryptocurrency, null);
            if (result.ResultCode == APIResultCodes.Success)
            {
                return await BlockBalanceAsync(accountId, klWallet.LastBlock);
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("MintNFT")]
        public async Task<BalanceResult> MintNFTAsync(string accountId, string name, string description, int supply, string metadataUrl)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.CreateNFTAsync(name, description, supply, metadataUrl);
            if (result.ResultCode == APIResultCodes.Success)
            {
                return await BlockBalanceAsync(accountId, klWallet.LastBlock);
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }
        
        [JsonRpcMethod("PrintFiat")]
        public async Task<BalanceResult> PrintFiatAsync(string accountId, string ticker, long amount)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.PrintFiatAsync(ticker, amount);
            if (result.ResultCode == APIResultCodes.Success)
            {
                return await BlockBalanceAsync(accountId, klWallet.LastBlock);
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("CreateTOT")]
        public async Task<string> CreateTotAsync(string accountId,
                string type,
                string name,
                string description,
                int supply,
                string tradeSecretSignature
            )
        {
            var wallet = await CreateWalletAsync(accountId);
            var acac = new AcademyClient(LyraNodeConfig.GetNetworkId());

            // try to sign the request
            var lsb = await wallet.RPC.GetLastServiceBlockAsync();
            var input = $"{wallet.AccountId}:{lsb.GetBlock().Hash}:{name}:{description}";
            var signature = await wallet.SignMsg(input);
            var totType = Enum.Parse<HoldTypes>(type);
            var retJson = await acac.CreateTotMetaAsync(wallet.AccountId, signature, totType, name, description);
            // the result format is compatible
            var dynret = JsonConvert.DeserializeObject<dynamic>(retJson);

            if (dynret.ret == "Success")
            {
                var metaUrl = dynret.result.ToString();
                APIResult ctret = await wallet.CreateTOTAsync(totType, name, description, supply, metaUrl, tradeSecretSignature);
                if (ctret.Successful())
                {
                    var totgen = wallet.GetLastSyncBlock() as TokenGenesisBlock;
                    return returnApiResult(ctret, totgen.Ticker);
                }
                else
                {
                    throw new Exception($"{ctret.ResultCode}: {ctret.ResultMessage}");
                }
            }
            else
            {
                throw new Exception(retJson);
            }
        }

        protected override bool GetIfInterested(string addr)
        {
            return addr == _monitorAccountId || (_monitorAccountId == "*" && Neo.Settings.Default.LyraNode.Lyra.Mode == NodeMode.App);
        }

        [JsonRpcMethod("Monitor")]
        public void Monitor(string accountId)
        {
            if (Signatures.ValidateAccountId(accountId) || (accountId == "*" && Neo.Settings.Default.LyraNode.Lyra.Mode == NodeMode.App))
            {
                _monitorAccountId = accountId;
            }
            else
            {
                _monitorAccountId = null;
            }
        }


        [JsonRpcMethod("History")]
        public async Task<List<TxDesc>> HistoryAsync(string accountId, long startTime, long endTime, int count)
        {
            // json time. convert it to dotnet time
            var dtStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) + new TimeSpan(startTime * 10000);
            var dtEnd = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc) + new TimeSpan(endTime * 10000);
            var hists = await _node.SearchTransactionsAsync(accountId, dtStart.Ticks, dtEnd.Ticks, count);
            if (hists.Successful())
                return hists.Transactions.Select(x => new TxDesc(x)).ToList();
            else
                throw new Exception($"{hists.ResultCode}: {hists.ResultMessage}");
        }

        // group pool

        [JsonRpcMethod("Pool")]
        public async Task<PoolInfo> PoolAsync(string token0, string token1)
        {
            var poolResult = await _node.GetPoolAsync(token0, token1);
            if (poolResult.Successful() && poolResult.PoolAccountId != null)
            {
                var poolLatest = await _node.GetLastBlockAsync(poolResult.PoolAccountId);
                if (poolLatest.Successful())
                {
                    var latestBlock = poolLatest.GetBlock() as TransactionBlock;
                    return new PoolInfo
                    {
                        poolId = poolResult.PoolAccountId,
                        height = latestBlock.Height,
                        token0 = poolResult.Token0,
                        token1 = poolResult.Token1,
                        balance = latestBlock.Balances.ToDecimalDict()
                    };
                }
            }

            throw new Exception("Failed to get pool");
        }

        [JsonRpcMethod("CreatePool")]
        public async Task<PoolInfo> CreatePoolAsync(string accountId, string token0, string token1)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.CreateLiquidatePoolAsync(token0, token1);
            if (result.ResultCode == APIResultCodes.Success)
            {
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);     // wait for the pool to be created.
                    try
                    {
                        return await PoolAsync(token0, token1);
                    }
                    catch { }
                }
                throw new Exception("Pool was not created properly.");
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("PoolCalculate")]
        public async Task<SwapCalculator> PoolCalculateAsync(string poolId, string swapFrom, decimal amount, decimal slippage)
        {
            var poolLatest = await NodeService.Dag.Storage.FindLatestBlockAsync(poolId) as TransactionBlock;
            var poolGenesis = await NodeService.Dag.Storage.FindFirstBlockAsync(poolId) as PoolGenesisBlock;

            if (poolLatest != null && poolGenesis != null && poolLatest.Hash != poolGenesis.Hash)
            {
                var cal = new SwapCalculator(poolGenesis.Token0, poolGenesis.Token1,
                    poolLatest, swapFrom, amount, slippage);
                return cal;
            }

            throw new Exception("Failed to get pool");
        }

        [JsonRpcMethod("AddLiquidaty")]
        public async Task<PoolInfo> AddLiquidatyAsync(string accountId, string token0, decimal token0Amount, string token1, decimal token1Amount)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var oldPool = await PoolAsync(token0, token1);
            var result = await klWallet.AddLiquidateToPoolAsync(token0, token0Amount, token1, token1Amount);
            if (result.ResultCode == APIResultCodes.Success)
            {
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);     // wait for the pool to be liquidated
                    try
                    {
                        var latestPool = await PoolAsync(token0, token1);
                        if (oldPool.height == latestPool.height)
                            continue;
                        else
                            return latestPool;
                    }
                    catch { }
                }
                throw new Exception("Add liquidaty failed.");
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("RemoveLiquidaty")]
        public async Task<BalanceResult> RemoveLiquidatyAsync(string accountId, string token0, string token1)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var oldPool = await PoolAsync(token0, token1);
            var result = await klWallet.RemoveLiquidateFromPoolAsync(token0, token1);
            if (result.ResultCode == APIResultCodes.Success)
            {
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);     // wait for the liquidaty to be removed
                    try
                    {
                        var latestPool = await PoolAsync(token0, token1);
                        if (oldPool.height == latestPool.height)
                            continue;
                        else
                        {
                            return await ReceiveAsync(accountId);
                        }
                    }
                    catch { }
                }
                throw new Exception("Remove liquidaty failed.");
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("Swap")]
        public async Task<BalanceResult> SwapAsync(string accountId, string token0, string token1, string tokenToSwap, decimal amountToSwap, decimal amountToGet)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var oldPool = await PoolAsync(token0, token1);
            var result = await klWallet.SwapTokenAsync(token0, token1, tokenToSwap, amountToSwap, amountToGet);
            if (result.ResultCode == APIResultCodes.Success)
            {
                for (int i = 0; i < 30; i++)
                {
                    await Task.Delay(500);     // wait for the swap to be finished
                    try
                    {
                        var latestPool = await PoolAsync(token0, token1);
                        if (oldPool.height == latestPool.height)
                            continue;
                        else
                        {
                            return await ReceiveAsync(accountId);
                        }
                    }
                    catch { }
                }
                throw new Exception("Token swap failed.");
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("GetBrokerAccounts")]
        public async Task<BrokerAccountsInfo> GetBrokerAccountsAsync(string accountId)
        {
            var result = await _node.GetAllBrokerAccountsForOwnerAsync(accountId);
            if (result.ResultCode == APIResultCodes.Success)
            {
                BrokerAccountsInfo accts = new BrokerAccountsInfo();
                accts.owner = accountId;
                accts.profits = new List<ProfitInfo>();
                accts.stakings = new List<StakingInfo>();

                var blks = result.GetBlocks();
                foreach (var blk in blks)
                {
                    if (blk is IProfiting pft)
                    {
                        var pftinfo = new ProfitInfo
                        {
                            owner = pft.OwnerAccountId,
                            pftid = (blk as TransactionBlock).AccountID,
                            seats = pft.Seats,
                            shareratio = pft.ShareRito,
                            name = pft.Name,
                            type = pft.PType.ToString()
                        };
                        accts.profits.Add(pftinfo);
                    }

                    if (blk is IStaking stk)
                    {
                        decimal amt = 0;
                        var lastblkret = await _node.GetLastBlockAsync((stk as TransactionBlock).AccountID);
                        TransactionBlock lastblk = null;
                        if (lastblkret.Successful())
                        {
                            lastblk = lastblkret.GetBlock() as TransactionBlock;
                            if (lastblk.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                            {
                                amt = lastblk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal();
                            }
                        }

                        var laststk = lastblk as IStaking;
                        var stkinfo = new StakingInfo
                        {
                            owner = stk.OwnerAccountId,
                            stkid = (stk as TransactionBlock).AccountID,
                            name = stk.Name,

                            start = laststk.Start,
                            amount = amt,
                            days = laststk.Days,
                            voting = laststk.Voting,
                            compound = laststk.CompoundMode
                        };

                        accts.stakings.Add(stkinfo);
                    }
                }
                return accts;
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("CreateProfitingAccount")]
        public async Task<ProfitInfo> CreateProfitingAccountAsync(string accountId, string Name, ProfitingType ptype, decimal shareRito, int maxVoter)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.CreateProfitingAccountAsync(Name, ptype, shareRito, maxVoter);
            if (result.ResultCode == APIResultCodes.Success)
            {
                var pgen = klWallet.LastBlock as ProfitingGenesis;
                var pftinfo = new ProfitInfo
                {
                    owner = pgen.OwnerAccountId,
                    pftid = pgen.AccountID,
                    seats = pgen.Seats,
                    shareratio = pgen.ShareRito,
                    name = pgen.Name,
                    type = pgen.PType.ToString()
                };

                return pftinfo;
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("CreateDividends")]
        public async Task<SimpleResult> CreateDividendsAsync(string accountId, string profitingAccountId)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.CreateDividendsAsync(profitingAccountId);
            if (result.ResultCode == APIResultCodes.Success)
            {
                return new SimpleResult { success = true };
            }
            else
            {
                return new SimpleResult
                {
                    success = false,
                    message = result.ResultCode.ToString()
                };
            }
        }

        [JsonRpcMethod("CreateStakingAccount")]
        public async Task<StakingInfo> CreateStakingAccountAsync(string accountId, string Name, string voteFor, int daysToStake, bool compoundMode)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.CreateStakingAccountAsync(Name, voteFor, daysToStake, compoundMode);
            if (result.ResultCode == APIResultCodes.Success)
            {
                var sgen = klWallet.LastBlock as StakingGenesis;
                var stkinfo = new StakingInfo
                {
                    owner = sgen.OwnerAccountId,
                    stkid = sgen.AccountID,
                    name = sgen.Name,

                    start = sgen.Start,
                    amount = 0,
                    days = sgen.Days,
                    voting = sgen.Voting,
                    compound = sgen.CompoundMode
                };

                return stkinfo;
            }
            else
            {
                throw new Exception($"{result.ResultCode}: {result.ResultMessage}");
            }
        }

        [JsonRpcMethod("AddStaking")]
        public async Task<SimpleResult> AddStakingAsync(string accountId, string stakingAccountId, decimal amount)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.AddStakingAsync(stakingAccountId, amount);
            if (result.ResultCode == APIResultCodes.Success)
            {
                return new SimpleResult { success = true };
            }
            else
            {
                return new SimpleResult
                {
                    success = false,
                    message = result.ResultCode.ToString()
                };
            }
        }

        [JsonRpcMethod("UnStaking")]
        public async Task<SimpleResult> UnStakingAsync(string accountId, string stakingAccountId)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.UnStakingAsync(stakingAccountId);
            if (result.ResultCode == APIResultCodes.Success)
            {
                return new SimpleResult { success = true };
            }
            else
            {
                return new SimpleResult
                {
                    success = false,
                    message = result.ResultCode.ToString()
                };
            }
        }

        [JsonRpcMethod("GetStaking")]
        public async Task<StakingInfo> GetStakingAsync(string accountId, string stakingAccountId)
        {
            var klWallet = await CreateWalletAsync(accountId);

            var result = await klWallet.GetStakingAsync(stakingAccountId);
            if (result != null)
            {
                var tb = result as TransactionBlock;
                var sgenresult = await _node.GetBlockByIndexAsync(stakingAccountId, 1);
                var sgen = sgenresult.GetBlock() as StakingGenesis;
                var stkinfo = new StakingInfo
                {
                    owner = sgen.OwnerAccountId,
                    stkid = sgen.AccountID,
                    name = sgen.Name,

                    start = sgen.Start,
                    amount = tb.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal(),
                    days = sgen.Days,
                    voting = sgen.Voting,
                    compound = sgen.CompoundMode
                };
                return stkinfo;
            }
            else
            {
                throw new Exception($"No such staking account");
            }
        }

        [JsonRpcMethod("GetPendingStats")]
        public async Task<PendingInfo> GetPendingStatsAsync(string accountId)
        {
            var ps = await _node.GetPendingStatsAsync(accountId);
            return new PendingInfo
            {
                accountid = ps.AccountId,
                funds = ps.PendingFunds,
                fees = ps.PendingFees
            };
        }

        [JsonRpcMethod("CreateOrder")]
        public async Task<string?> CreateOrderAsync(string accountId, string order)
        {
            try
            {
                var klWallet = await CreateWalletAsync(accountId);
                var argsObj = JObject.Parse(order);
                var argsDict = argsObj.ToObject<Dictionary<string, string>>();
                if (argsDict != null)
                {
                    var orderObj = new UniOrder
                    {
                        daoId = argsDict["daoid"],
                        dealerId = argsDict["dealerid"],
                        offerby = LyraGlobal.GetHoldTypeFromTicker(argsDict["selltoken"]),
                        offering = argsDict["selltoken"],
                        bidby = LyraGlobal.GetHoldTypeFromTicker(argsDict["gettoken"]),
                        biding = argsDict["gettoken"],
                        price = decimal.Parse(argsDict["price"]),
                        cltamt = decimal.Parse(argsDict["collateral"]),
                        payBy = new string[0],

                        amount = decimal.Parse(argsDict["count"]),
                        limitMin = decimal.Parse(argsDict["limitmin"]),
                        limitMax = decimal.Parse(argsDict["limitmax"]),
                    };

                    var ret = await klWallet.CreateUniOrderAsync(orderObj);
                    if(!ret.Successful()) return returnError(ret.ResultMessage);

                    // wait for complete
                    klWallet.WaitForWorkflow(ret.TxHash, 30000);

                    return returnApiResult(ret, ret.TxHash);
                }
                else
                {
                    throw new Exception("Invalid order data");
                }
            }
            catch (Exception ex)
            {
                return returnError(ex.Message);
            }
        }

        [JsonRpcMethod("CreateTrade")]
        public async Task<string?> CreateTradeAsync(string accountId, string tradeJson)
        {
            try
            {
                var klWallet = await CreateWalletAsync(accountId);
                var argsObj = JObject.Parse(tradeJson);
                var argsDict = argsObj.ToObject<Dictionary<string, string>>();
                if (argsDict != null)
                {
                    var tradeObj = new UniTrade
                    {
                        daoId = argsDict["daoId"],
                        dealerId = argsDict["dealerId"],
                        orderId = argsDict["orderId"],
                        orderOwnerId = argsDict["orderOwnerId"],

                        offby = LyraGlobal.GetHoldTypeFromTicker(argsDict["offby"]),
                        offering = argsDict["offering"],
                        bidby = LyraGlobal.GetHoldTypeFromTicker(argsDict["bidby"]),
                        biding = argsDict["biding"],

                        price = decimal.Parse(argsDict["price"]),
                        cltamt = decimal.Parse(argsDict["cltamt"]),
                        payVia = argsDict["payVia"],
                        amount = decimal.Parse(argsDict["amount"]),
                        pay = decimal.Parse(argsDict["pay"]),
                    };

                    var ret = await klWallet.CreateUniTradeAsync(tradeObj);
                    if (!ret.Successful()) return returnError(ret.ResultMessage);

                    // wait for complete
                    klWallet.WaitForWorkflow(ret.TxHash, 30000);

                    return returnApiResult(ret, ret.TxHash);
                }
                else
                {
                    throw new Exception("Invalid order data");
                }
            }
            catch (Exception ex)
            {
                return returnError(ex.Message);
            }
        }

        // common return types
        private static string returnError(string errorMsg)
        {
            return JsonConvert.SerializeObject(
            new
            {
                ret = "Error",
                msg = errorMsg
            });
        }

        private static string returnSuccess(object result)
        {
            return JsonConvert.SerializeObject(
            new
            {
                ret = "Success",
                result
            });
        }

        private static string returnApiResult(APIResult result)
        {
            return JsonConvert.SerializeObject(
            new
            {
                ret = result.Successful() ? "Success" : "Error",
                msg = result.ResultMessage ?? result.ResultCode.Humanize(),
            });
        }

        private static string returnApiResult(APIResult result, object payload)
        {
            return JsonConvert.SerializeObject(
            new
            {
                ret = result.Successful() ? "Success" : "Error",
                msg = result.ResultMessage ?? result.ResultCode.Humanize(),
                result = payload,
            });
        }
    }
}
