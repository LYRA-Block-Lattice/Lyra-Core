using Lyra.Core.Accounts;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.States.Holding
{
    class Actions
    {
    }

    public class OpenWallet
    {
        public IAccountDatabase database { get; set; }
        public string network { get; set; }
        public string walletPath { get; set; }
        public string walletName { get; set; }
        public string apiUrl { get; set; }
    }

    public class CloseWallet
    {

    }

    public class Send
    {
        public string address { get; set; }
        public decimal amount { get; set; }
    }

    public class SendExchange : Send
    {

    }

    public class Receive
    {

    }

    public class RefreshBalances
    {

    }
}
