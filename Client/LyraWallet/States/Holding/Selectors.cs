using ReduxSimple;
using static ReduxSimple.Selectors;

namespace LyraWallet.States.Holding
{
    public static class Selectors
    {
        public static ISelectorWithoutProps<RootState, HoldingState> SelectWalletState = CreateSelector(
            (RootState state) => state.walletState
        );

        public static ISelectorWithoutProps<RootState, Lyra.Core.Accounts.Wallet> SelectWallet = CreateSelector(
            SelectWalletState,
            state => state.lyraWallet
        );
    }
}
