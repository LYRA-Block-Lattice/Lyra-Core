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
        public Wallet lyraWallet { get; set; }
        public string apiUrl { get; set; }
        public LyraRestClient restClient {get; set;}
        public LyraRestNotify notifyClient { get; set; }

        public CancellationTokenSource cancel { get; set; }

        public static HoldingState InitialState =>
            new HoldingState
            {
                lyraWallet = null,
                restClient = null,
                notifyClient = null
            };
    }
}
