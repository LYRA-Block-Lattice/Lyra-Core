using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.States
{
    public class ExchangeState
    {
        public int order;
        public static ExchangeState InitialState =>
            new ExchangeState
            {

            };
    }
}
