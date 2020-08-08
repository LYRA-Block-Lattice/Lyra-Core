using Lyra.Core.Accounts;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
	public class WebWalletState
	{
		public bool IsOpening { get; }
		public Wallet wallet { get; }

		public WebWalletState(bool isLoading, Wallet wallet)
		{
			IsOpening = isLoading;
			this.wallet = wallet ?? null;
		}
	}
}
