using ReduxSimple;
using static ReduxSimple.Selectors;

namespace LyraWallet.States.Exchange
{
    public static class Selectors
    {
        public static ISelectorWithoutProps<RootState, ExchangeState> SelectExchangeState = CreateSelector(
            (RootState state) => state.exchangeState
        );

        public static ISelectorWithoutProps<RootState, int> SelectWallet = CreateSelector(
            SelectExchangeState,
            state => state.order
        );
    }
}
