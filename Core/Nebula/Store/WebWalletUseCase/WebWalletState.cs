using DotNetty.Codecs;
using Lyra.Core.Accounts;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
	public enum UIStage { Entry, Main, Send, Settings, Transactions, FreeToken };

	public class WebWalletState
	{
		public UIStage stage { get; }
		public bool IsOpening { get; }
		public Wallet wallet { get; }
		public List<string> txs { get; }
		public decimal faucetBalance { get; }

		public WebWalletState(bool IsOpeing, Wallet wallet, UIStage Stage, List<string> transactions = null, decimal faucetBalance = 0)
		{
			this.IsOpening = IsOpeing;
			this.wallet = wallet ?? null;
			this.stage = Stage;
			this.txs = transactions;
			this.faucetBalance = faucetBalance;
		}
	}
}
