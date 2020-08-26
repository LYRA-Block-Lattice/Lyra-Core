using Lyra.Core.Accounts;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
	public enum UIStage { Entry, Main, Send, Settings };

	public class WebWalletState
	{
		public UIStage stage { get; }
		public bool IsOpening { get; }
		public Wallet wallet { get; }

		public WebWalletState(bool IsOpeing, Wallet wallet, UIStage Stage)
		{
			this.IsOpening = IsOpeing;
			this.wallet = wallet ?? null;
			this.stage = Stage;
		}
	}
}
