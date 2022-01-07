using DexServer.Ext;
using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow
{
    [LyraWorkFlow]
    public class WFDexDeposit : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DEX_DPOREQ,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.DexWalletGenesis,
                        TheBlock = typeof(DexWalletGenesis),
                        //AuthorizerType = typeof(DexWalletGenesisAuthorizer),
                    }
                }
            };
        }

        // DEX
        #region BRK_DEX_DPOREQ
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock last)
        {
            var symbol = block.Tags.ContainsKey("symbol") ? block.Tags["symbol"] : null;
            if (symbol == null)
                return APIResultCodes.InvalidName;

            var provider = block.Tags.ContainsKey("provider") ? block.Tags["provider"] : null;
            if (provider == null)
                return APIResultCodes.InvalidName;

            if (block.Tags.Count > 3)
                return APIResultCodes.InvalidBlockTags;

            var dc = new DexClient(LyraNodeConfig.GetNetworkId());
            var asts = await dc.GetSupportedExtTokenAsync(LyraNodeConfig.GetNetworkId());

            var ast = asts.Asserts.Where(a => a.Symbol == symbol && a.NetworkProvider == provider)
                .FirstOrDefault();
            if (ast == null)
                return APIResultCodes.InvalidExternalToken;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            var symbol = send.Tags["symbol"];
            var provider = send.Tags["provider"];

            // request a wallet from dex server
            var dc = new DexClient(LyraNodeConfig.GetNetworkId());
            var r1 = await dc.CreateWalletAsync(send.AccountID, symbol, provider,
                send.Hash,
                NodeService.Dag.PosWallet.AccountId,
                Signatures.GetSignature(NodeService.Dag.PosWallet.PrivateKey, send.Hash, NodeService.Dag.PosWallet.AccountId)
                );
            if (!r1.Success)
                throw new Exception("DEX Server failed: " + r1.Message);

            var extw = r1 as DexAddress;
            //Assert.IsTrue(extw.Address.StartsWith('T'));

            var keyStr = $"{send.Hash.Substring(0, 16)},{symbol},{provider},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var exists = await sys.Storage.FindDexWalletAsync(send.AccountID, symbol, provider);
            if (exists != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var wgen = new DexWalletGenesis
            {
                Height = 1,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountID = AccountId,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),

                // broker
                Name = symbol + (String.IsNullOrEmpty(provider) ? "" : $" via {provider}"),
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // Dex wallet
                IntSymbol = $"${symbol}",
                ExtSymbol = symbol,
                ExtProvider = provider,
                ExtAddress = extw.Address,

                // genesis
                AccountType = AccountTypes.DEX
            };

            wgen.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            wgen.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return wgen;
        }
    }

    [LyraWorkFlow]
    public class WFDexMint : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DEX_MINT,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.DexTokenMint,
                        TheBlock = typeof(TokenMintBlock),
                        //AuthorizerType = typeof(DexTokenMintAuthorizer),
                    },
                    new BlockDesc
                    {
                        BlockType = BlockTypes.DexTokenBurn,
                        TheBlock = typeof(TokenBurnBlock),
                        //AuthorizerType = typeof(DexTokenBurnAuthorizer),
                    }
                }
            };
        }

        #endregion

        #region BRK_DEX_MINT
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock last)
        {
            var dexid = block.Tags.ContainsKey("dexid") ? block.Tags["dexid"] : null;
            if (dexid == null)
                return APIResultCodes.InvalidAccountId;

            decimal mintamount = 0;
            var mintamountstr = block.Tags.ContainsKey("amount") ? block.Tags["amount"] : null;
            if (mintamountstr == null || !decimal.TryParse(mintamountstr, out mintamount) || mintamount <= 0)
                return APIResultCodes.InvalidAmount;

            // verify if sender is dex server
            if (block.AccountID != LyraGlobal.GetDexServerAccountID(LyraNodeConfig.GetNetworkId()))
                return APIResultCodes.InvalidDexServer;

            // verify dex wallet owner
            var brkr = await sys.Storage.FindLatestBlockAsync(dexid) as IBrokerAccount;
            if (brkr == null)
                return APIResultCodes.InvalidBrokerAcount;

            return APIResultCodes.Success;
        }
        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is TokenMintBlock))
                return null;

            var dexid = send.Tags["dexid"];
            var amount = long.Parse(send.Tags["amount"]).ToBalanceDecimal();

            var last = await sys.Storage.FindLatestBlockAsync(dexid) as TransactionBlock;
            var lastdex = last as IDexWallet;

            var ticker = $"tether/{lastdex.ExtSymbol}";
            var gensis = await sys.Storage.FindTokenGenesisBlockAsync(null, ticker);
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var mint = new TokenMintBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountID = last.AccountID,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),

                // broker
                Name = lastdex.Name,
                OwnerAccountId = lastdex.OwnerAccountId,
                RelatedTx = send.Hash,

                // Dex wallet
                IntSymbol = lastdex.IntSymbol,
                ExtSymbol = lastdex.ExtSymbol,
                ExtProvider = lastdex.ExtProvider,
                ExtAddress = lastdex.ExtAddress,

                // mint
                MintBy = send.AccountID,
                GenesisHash = gensis.Hash,
                MintAmount = amount.ToBalanceLong()
            };

            mint.Balances = last.Balances.ToDecimalDict().ToLongDict();
            if (mint.Balances.ContainsKey(ticker))
                mint.Balances[ticker] += amount.ToBalanceLong();
            else
                mint.Balances.Add(ticker, amount.ToBalanceLong());

            mint.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            mint.InitializeBlock(last, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return mint;
        }
        #endregion
    }

    /// <summary>
    /// user get token = dex server send token
    /// </summary>
    [LyraWorkFlow]
    public class WFDexGetToken : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DEX_GETTKN,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.DexSendToken,
                        TheBlock = typeof(DexSendBlock),
                        //AuthorizerType = typeof(DexSendAuthorizer),
                    }
                }
            };
        }

        #region BRK_DEX_GETTKN
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock last)
        {
            var dexid2 = block.Tags.ContainsKey("dexid") ? block.Tags["dexid"] : null;
            if (dexid2 == null)
                return APIResultCodes.InvalidAccountId;

            decimal amountToGet = 0;
            var amountToGetStr = block.Tags.ContainsKey("amount") ? block.Tags["amount"] : null;
            if (amountToGetStr == null || !decimal.TryParse(amountToGetStr, out amountToGet) || amountToGet <= 0)
                return APIResultCodes.InvalidAmount;

            // verify owner
            var lb = await sys.Storage.FindLatestBlockAsync(dexid2) as IDexWallet;
            if (lb == null || block.AccountID != lb.OwnerAccountId)
                return APIResultCodes.InvalidAccountId;

            var ticker = $"tether/{lb.ExtSymbol}";

            var lbtx = lb as TransactionBlock;
            if (lbtx == null || !lbtx.Balances.ContainsKey(ticker)
                || lbtx.Balances[ticker] < amountToGet)
                return APIResultCodes.InvalidAmount;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is DexSendBlock))
                return null;

            var dexid = send.Tags["dexid"];
            var amount = long.Parse(send.Tags["amount"]).ToBalanceDecimal();

            var last = await sys.Storage.FindLatestBlockAsync(dexid) as TransactionBlock;
            var lastdex = last as IDexWallet;

            var ticker = $"tether/{lastdex.ExtSymbol}";
            var gensis = await sys.Storage.FindTokenGenesisBlockAsync(null, ticker);
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendtoken = new DexSendBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountID = last.AccountID,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),

                // broker
                Name = lastdex.Name,
                OwnerAccountId = lastdex.OwnerAccountId,
                RelatedTx = send.Hash,

                // Dex wallet
                IntSymbol = lastdex.IntSymbol,
                ExtSymbol = lastdex.ExtSymbol,
                ExtProvider = lastdex.ExtProvider,
                ExtAddress = lastdex.ExtAddress,

                // send
                DestinationAccountId = send.AccountID
            };

            sendtoken.Balances = last.Balances.ToDecimalDict().ToLongDict();
            sendtoken.Balances[ticker] -= amount.ToBalanceLong();

            sendtoken.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            sendtoken.InitializeBlock(last, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return sendtoken;
        }
        #endregion
    }

    [LyraWorkFlow]
    public class WFDexPutToken : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DEX_PUTTKN,
                RecvVia = BrokerRecvType.None,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.DexRecvToken,
                        TheBlock = typeof(DexReceiveBlock),
                        //AuthorizerType = typeof(DexReceiveAuthorizer),
                    }
                }
            };
        }

        #region BRK_DEX_PUTTKN
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock last)
        {
            var dexid3 = block.Tags.ContainsKey("dexid") ? block.Tags["dexid"] : null;
            if (dexid3 == null)
                return APIResultCodes.InvalidAccountId;

            // verify owner
            var lb3 = await sys.Storage.FindLatestBlockAsync(dexid3) as IDexWallet;
            if (lb3 == null || block.AccountID != lb3.OwnerAccountId)
                return APIResultCodes.InvalidAccountId;

            var tickerp = $"tether/{lb3.ExtSymbol}";

            var userlb = await sys.Storage.FindLatestBlockAsync(block.AccountID) as TransactionBlock;
            if (userlb == null || !userlb.Balances.ContainsKey(tickerp))
                return APIResultCodes.InvalidAmount;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is DexReceiveBlock))
                return null;

            var dexid = send.Tags["dexid"];

            var last = await sys.Storage.FindLatestBlockAsync(dexid) as TransactionBlock;
            var lastdex = last as IDexWallet;

            var ticker = $"tether/{lastdex.ExtSymbol}";
            var gensis = await sys.Storage.FindTokenGenesisBlockAsync(null, ticker);

            var prev = await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;
            var chgs = send.GetBalanceChanges(prev);

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var recvtoken = new DexReceiveBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountID = last.AccountID,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),

                // broker
                Name = lastdex.Name,
                OwnerAccountId = lastdex.OwnerAccountId,
                RelatedTx = send.Hash,

                // Dex wallet
                IntSymbol = lastdex.IntSymbol,
                ExtSymbol = lastdex.ExtSymbol,
                ExtProvider = lastdex.ExtProvider,
                ExtAddress = lastdex.ExtAddress,

                // Receive
                SourceHash = send.Hash
            };

            recvtoken.Balances = last.Balances.ToDecimalDict().ToLongDict();
            if (recvtoken.Balances.ContainsKey(ticker))
            {
                recvtoken.Balances[ticker] += chgs.Changes[ticker].ToBalanceLong();
            }
            else
            {
                recvtoken.Balances.Add(ticker, chgs.Changes[ticker].ToBalanceLong());
            }

            recvtoken.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            recvtoken.InitializeBlock(last, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return recvtoken;
        }
        #endregion
    }

    [LyraWorkFlow]
    public class WFDexWithdraw : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_DEX_WDWREQ,
                RecvVia = BrokerRecvType.PFRecv,
                Blocks = new[] {
                    new BlockDesc
                    {
                        BlockType = BlockTypes.DexWithdrawToken,
                        TheBlock = typeof(TokenWithdrawBlock),
                        //AuthorizerType = typeof(DexWithdrawAuthorizer),
                    }
                }
            };
        }

        #region BRK_DEX_WDWREQ
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock block, TransactionBlock last)
        {
            var dexid4 = block.Tags.ContainsKey("dexid") ? block.Tags["dexid"] : null;
            if (dexid4 == null)
                return APIResultCodes.InvalidAccountId;

            decimal wdwmount4 = 0;
            var wdwamountstr4 = block.Tags.ContainsKey("amount") ? block.Tags["amount"] : null;
            if (wdwamountstr4 == null || !decimal.TryParse(wdwamountstr4, out wdwmount4) || wdwmount4 <= 0)
                return APIResultCodes.InvalidAmount;

            // verify owner
            var lb4 = await sys.Storage.FindLatestBlockAsync(dexid4) as IBrokerAccount;
            if (lb4 == null || block.AccountID != lb4.OwnerAccountId)
                return APIResultCodes.InvalidAccountId;

            var extaddr = block.Tags.ContainsKey("extaddr") ? block.Tags["extaddr"] : null;
            if (string.IsNullOrWhiteSpace(extaddr))
                return APIResultCodes.InvalidExternalAddress;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            if (blocks.Any(a => a is TokenBurnBlock))
                return null;

            var dexid = send.Tags["dexid"];
            var extaddr = send.Tags["extaddr"];
            var amount = long.Parse(send.Tags["amount"]).ToBalanceDecimal();

            var last = await sys.Storage.FindLatestBlockAsync(dexid) as TransactionBlock;
            var lastdex = last as IDexWallet;

            var ticker = $"tether/{lastdex.ExtSymbol}";
            var gensis = await sys.Storage.FindTokenGenesisBlockAsync(null, ticker);
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var burntoken = new TokenWithdrawBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountID = last.AccountID,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),

                // broker
                Name = lastdex.Name,
                OwnerAccountId = lastdex.OwnerAccountId,
                RelatedTx = send.Hash,

                // Dex wallet
                IntSymbol = lastdex.IntSymbol,
                ExtSymbol = lastdex.ExtSymbol,
                ExtProvider = lastdex.ExtProvider,
                ExtAddress = lastdex.ExtAddress,

                // Burn
                BurnBy = sb.Leader,
                GenesisHash = gensis.Hash,
                BurnAmount = amount.ToBalanceLong(),

                // withdraw
                WithdrawToExtAddress = extaddr,
            };

            burntoken.Balances = last.Balances.ToDecimalDict().ToLongDict();
            burntoken.Balances[ticker] -= amount.ToBalanceLong();

            burntoken.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            burntoken.InitializeBlock(last, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            return burntoken;
        }

        public override async Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string hash)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(hash);
            if (!blocks.Any(a => a is TokenWithdrawBlock))
                throw new Exception($"TokenWithdrawBlock not found.");

            var burnblock = blocks.Where(a => a is TokenWithdrawBlock).First();
            var burnbrk = burnblock as IBrokerAccount;
            var burn = burnblock as TokenWithdrawBlock;

            var dc = new DexClient(LyraNodeConfig.GetNetworkId());
            var ret = await dc.RequestWithdrawAsync(burnbrk.OwnerAccountId, burn.ExtSymbol, burn.ExtProvider,
                burn.AccountID, hash,
                burn.WithdrawToExtAddress, burn.BurnAmount,
                NodeService.Dag.PosWallet.AccountId,
                Signatures.GetSignature(NodeService.Dag.PosWallet.PrivateKey, hash, NodeService.Dag.PosWallet.AccountId));

            if (!ret.Success)
                throw new Exception($"Error RequestWithdrawAsync to DEX Server: {ret.Message}");

            return null;
        }

        #endregion
    }
}
