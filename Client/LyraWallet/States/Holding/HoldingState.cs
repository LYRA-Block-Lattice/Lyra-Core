using Lyra.Core.Accounts;
using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace LyraWallet.States.Holding
{
    public class HoldingState
    {
        public string AccountID { get; set; }
        public string PrivateKey { get; set; }

        public BalanceEntityState Balances { get; set; }

        public static HoldingState InitialState =>
            new HoldingState
            {
                Balances = new BalanceEntityState(),
                AccountID = null,
                PrivateKey = null
            };
    }
}
