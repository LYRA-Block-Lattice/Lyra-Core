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
		private const string faucetKey = "freeLyraToken";
		// for faucet
		public int? freeTokenTimes { get; set; }

		public UIStage stage { get; set; } = UIStage.Entry;
		public bool IsOpening { get; set; } = false;
		public Wallet wallet { get; set; } = null;
		public List<string> txs { get; set; } = null;
		public decimal faucetBalance { get; set; } = 0m;
		public bool freeTokenSent { get; set; } = false;

		public bool ValidReCAPTCHA { get; set; } = false;

		public bool ServerVerificatiing { get; set; } = false;

		public bool DisablePostButton => !ValidReCAPTCHA || ServerVerificatiing;
	}
}
