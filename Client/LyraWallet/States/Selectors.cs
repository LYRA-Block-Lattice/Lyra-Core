using ReduxSimple;
using System;
using System.Collections.Generic;
using System.Text;
using static ReduxSimple.Selectors;

namespace LyraWallet.States
{
    public static class Selectors
    {
        public static ISelectorWithoutProps<RootState, bool> SelectIsOpening = CreateSelector
            ((RootState state) => state.IsOpening);
    }
}
