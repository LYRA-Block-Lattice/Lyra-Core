using ReduxSimple;
using static ReduxSimple.Selectors;

namespace LyraWallet.States.Holding
{
    public static class Selectors
    {
        public static ISelectorWithoutProps<RootState, HoldingState> SelectWalletState = CreateSelector(
            (RootState state) => state.walletState
        );

        public static ISelectorWithoutProps<RootState, string> SelectAccountId = CreateSelector(
            SelectWalletState,
            state => state.AccountID
        );

        public static ISelectorWithoutProps<RootState, string> SelectPrivateKey = CreateSelector(
            SelectWalletState,
            state => state.PrivateKey
        );
        public static ISelectorWithoutProps<RootState, BalanceEntityState> SelectBalanceEntityState = CreateSelector(
            SelectWalletState,
            state => state.Balances
        );
    }
}
