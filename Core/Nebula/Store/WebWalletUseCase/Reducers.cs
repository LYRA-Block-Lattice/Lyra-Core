using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
	public static class Reducers
	{
		[ReducerMethod]
		public static WebWalletState ReduceFetchDataAction(WebWalletState state, WebWalletAction action) =>
			new WebWalletState(
				isLoading: true,
				wallet: null);

		[ReducerMethod]
		public static WebWalletState ReduceFetchDataResultAction(WebWalletState state, WebWalletResultAction action) =>
			new WebWalletState(
				isLoading: false,
				wallet: action.Forecasts);
	}
}
