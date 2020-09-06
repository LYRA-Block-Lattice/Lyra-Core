using Converto;
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
		public static WebWalletState CloseAction(WebWalletState state, WebWalletCloseAction action) => new WebWalletState();

		[ReducerMethod]
		public static WebWalletState SendAction(WebWalletState state, WebWalletSendAction action) => state.With(new { stage = UIStage.Send });

		[ReducerMethod]
		public static WebWalletState CancelSendAction(WebWalletState state, WebWalletCancelSendAction action) => state.With(new { stage = UIStage.Main });

		[ReducerMethod]
		public static WebWalletState ReduceFetchDataResultAction(WebWalletState state, WebWalletResultAction action) => state.With(new { 
			stage = action.stage,
			IsOpening = action.IsOpening,
			wallet = action.wallet
		});

		[ReducerMethod]
		public static WebWalletState ReduceOpenSettingsAction(WebWalletState state, WebWalletSettingsAction action) => state.With(new { stage = UIStage.Settings });

		[ReducerMethod]
		public static WebWalletState ReduceSaveSettingsAction(WebWalletState state, WebWalletSaveSettingsAction action)
		{
            var state2 = state.With(new
            {
                stage = UIStage.Main,
            });
			state2.wallet.VoteFor = action.VoteFor;
			return state2;
        }

		[ReducerMethod]
		public static WebWalletState ReduceCancelSaveSettingsAction(WebWalletState state, WebWalletCancelSaveSettingsAction action) => state.With(new { stage = UIStage.Main });

		[ReducerMethod]
		public static WebWalletState ReduceTransactionsAction(WebWalletState state, WebWalletTransactionsResultAction action) =>
			state.With(new {
				stage = UIStage.Transactions,
				txs = action.transactions
			});

		[ReducerMethod]
		public static WebWalletState ReduceFreeTokenAction(WebWalletState state, WebWalletFreeTokenResultAction action) =>
			state.With(new { 
				stage = UIStage.FreeToken,
				faucetBalance = action.faucetBalance,
				ValidReCAPTCHA = false,
				ServerVerificatiing = false
			});

		[ReducerMethod]
		public static WebWalletState ReduceSendMeFreeTokenAction(WebWalletState state, WebWalletSendMeFreeTokenResultAction action)
        {
			var stt = state.With(new { 
				stage = UIStage.Main
			});

			if (action.Success)
			{
				stt.freeTokenSent = true;
				stt.freeTokenTimes++;
			}
			return stt;
		}

		[ReducerMethod]
		public static WebWalletState ReduceRCValidAction(WebWalletState state, WebWalletReCAPTCHAValidAction action) => state.With(new { ValidReCAPTCHA = action.ValidReCAPTCHA });

		[ReducerMethod]
		public static WebWalletState ReduceRCServerAction(WebWalletState state, WebWalletReCAPTCHAServerAction action) => state.With(new { ServerVerificatiing = action.ServerVerificatiing });
	}
}
