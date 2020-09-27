using Lyra.Core.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
    public class WebWalletCreateAction { }

    public class WebWalletRestoreAction { 
        public string privateKey { get; set; } 
        public bool selfVote { get; set; }
    }

    public class WebWalletCloseAction { }

    public class WebWalletRefreshBalanceAction { public Wallet wallet { get; set; } }

    public class WebWalletSendAction { }

    public class WebWalletSendTokenAction
    {
        public Wallet wallet { get; set; }
        public string DstAddr { get; set; }
        public string TokenName { get; set; }
        public decimal Amount { get; set; }
    }

    public class WebWalletCancelSendAction { }

    public class WebWalletSettngsAction { }

    public class WebWalletCreateTokenAction { }

    public class WebWalletSettingsAction { }

    public class WebWalletSaveSettingsAction
    {
        public string VoteFor { get; set; }
    }

    public class WebWalletCancelSaveSettingsAction { }

    public class WebWalletTransactionsAction
    {
        public Wallet wallet { get; set; }
    }
    public class WebWalletTransactionsResultAction
    {
        public Wallet wallet { get; set; }
        public List<string> transactions { get; set; }
    }

    public class WebWalletFreeTokenAction
    {
        public string faucetPvk { get; set; }
    }
    public class WebWalletFreeTokenResultAction
    {
        public decimal faucetBalance { get; set; }
    }
    public class WebWalletSendMeFreeTokenAction
    {
        public Wallet wallet { get; set; }
        public string faucetPvk { get; set; }
    }

    public class WebWalletSendMeFreeTokenResultAction
    {
        public bool Success { get; set; }
        public decimal FreeAmount { get; set; }
    }

    public class WebWalletReCAPTCHAValidAction
    {
        public bool ValidReCAPTCHA { get; set; }
    }

    public class WebWalletReCAPTCHAServerAction
    {
        public bool ServerVerificatiing { get; set; }
    }
}
