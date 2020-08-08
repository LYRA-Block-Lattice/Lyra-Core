using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
	public class Feature : Feature<WebWalletState>
	{
		public override string GetName() => "Weather";
		protected override WebWalletState GetInitialState() =>
			new WebWalletState(
				isLoading: false,
				wallet: null);
	}
}
