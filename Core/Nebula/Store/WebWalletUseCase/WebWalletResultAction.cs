using Lyra.Core.Accounts;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
	public class WebWalletResultAction
	{
		public Wallet Forecasts { get; }

		public WebWalletResultAction(Wallet forecasts)
		{
			Forecasts = forecasts;
		}
	}
}
