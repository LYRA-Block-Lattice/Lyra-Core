using Lyra.Core.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WebWalletUseCase
{
    public class WebWalletCreateAction { }

    public class WebWalletRestoreAction { public string privateKey { get; set; } }

    public class WebWalletCloseAction { }

    public class WebWalletRefreshBalanceAction { public Wallet wallet { get; set; } }

    public class WebWalletSendAction { }

    public class WebWalletSendTokenAction {
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
}
