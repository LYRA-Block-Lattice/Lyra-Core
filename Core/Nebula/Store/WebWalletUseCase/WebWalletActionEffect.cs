using Fluxor;
using Microsoft.AspNetCore.Components;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;
using Lyra.Core.API;
using Microsoft.Extensions.Configuration;
using System.IO;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;

namespace Nebula.Store.WebWalletUseCase
{
	public class Effects
	{
		private readonly LyraRestClient client;
		private readonly IConfiguration config;

		public Effects(LyraRestClient lyraClient, IConfiguration configuration)
		{
			client = lyraClient;
			config = configuration;
		}

        [EffectMethod]
        public async Task HandleSend(WebWalletSendTokenAction action, IDispatcher dispatcher)
        {
			var result = await action.wallet.Send(action.Amount, action.DstAddr, action.TokenName);
			if(result.ResultCode == Lyra.Core.Blocks.APIResultCodes.Success)
            {

            }
            dispatcher.Dispatch(new WebWalletResultAction(action.wallet, true, UIStage.Main));
        }

        [EffectMethod]
		public async Task HandleCreation(WebWalletCreateAction action, IDispatcher dispatcher)
		{
			var store = new AccountInMemoryStorage();
			var name = Guid.NewGuid().ToString();
			Wallet.Create(store, name, "", config["network"]);

			var wallet = Wallet.Open(store, name, "");
			await wallet.Sync(client);

			dispatcher.Dispatch(new WebWalletResultAction(wallet, true, UIStage.Main));
		}

		[EffectMethod]
		protected async Task HandleRestore(WebWalletRestoreAction action, IDispatcher dispatcher)
		{
			var store = new AccountInMemoryStorage();
			var name = Guid.NewGuid().ToString();
			Wallet.Create(store, name, "", config["network"], action.privateKey);

			var wallet = Wallet.Open(store, name, "");
			await wallet.Sync(client);

			dispatcher.Dispatch(new WebWalletResultAction(wallet, true, UIStage.Main));
		}

		[EffectMethod]
		protected async Task HandleRefresh(WebWalletRefreshBalanceAction action, IDispatcher dispatcher)
		{
			var result = await action.wallet.Sync(null);
			if (result == Lyra.Core.Blocks.APIResultCodes.Success)
			{

			}
			dispatcher.Dispatch(new WebWalletResultAction(action.wallet, true, UIStage.Main));
		}

		[EffectMethod]
		protected async Task HandleTransactions(WebWalletTransactionsAction action, IDispatcher dispatcher)
		{
			var result = await action.wallet.Sync(null);
			List<string> txs = new List<string>();
			if (result == Lyra.Core.Blocks.APIResultCodes.Success)
			{
				var accHeight = await client.GetAccountHeight(action.wallet.AccountId);
				Dictionary<string, long> oldBalance = null;
				for(long i = 1; i <= accHeight.Height; i++)
                {
					var blockResult = await client.GetBlockByIndex(action.wallet.AccountId, i);
					var block = blockResult.GetBlock() as TransactionBlock;
					if (block == null)
						txs.Add("Null");
					else
                    {
						var str = $"No. {block.Height} {block.TimeStamp}, ";
						if (block is SendTransferBlock sb)
							str += $"Send to {sb.DestinationAccountId}";
						else if(block is ReceiveTransferBlock rb)
                        {
							if(rb.SourceHash == null)
                            {
								str += $"Genesis";
                            }
							else
                            {
								var srcBlockResult = await client.GetBlock(rb.SourceHash);
								var srcBlock = srcBlockResult.GetBlock() as TransactionBlock;
								str += $"Receive from {srcBlock.AccountID}";
							}
						}
						str += BalanceDifference(oldBalance, block.Balances);
						str += $" Balance: {string.Join(", ", block.Balances.Select(m => $"{m.Key}: {m.Value / LyraGlobal.TOKENSTORAGERITO}"))}";
							
						txs.Add(str);

						oldBalance = block.Balances;
					}					
                }
			}
			txs.Reverse();
			dispatcher.Dispatch(new WebWalletTransactionsResultAction { wallet = action.wallet, transactions = txs });
		}

		private string BalanceDifference(Dictionary<string, long> oldBalance, Dictionary<string, long> newBalance)
        {
			if(oldBalance == null)
            {
				return " Amount: " + string.Join(", ", newBalance.Select(m => $"{m.Key} {m.Value / LyraGlobal.TOKENSTORAGERITO}"));
			}
			else
            {
				return " Amount: " + string.Join(", ", newBalance.Select(m => $"{m.Key} {(m.Value - (oldBalance.ContainsKey(m.Key) ? oldBalance[m.Key] : 0)) / LyraGlobal.TOKENSTORAGERITO}"));               
            }
        }
	}
}
