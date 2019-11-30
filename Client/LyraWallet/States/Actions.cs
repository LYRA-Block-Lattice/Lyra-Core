using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.States
{
    public class GetApiVersionAction
    {
        public string Network { get; set; }
        public string AppName { get; set; }
        public string AppVersion { get; set; }
    }

    public class GetApiVersionSuccessAction
    {
        public bool UpgradeNeeded { get; set; }
        public bool MustUpgradeToConnect { get; set; }
    }

    public class GetApiVersionFailedAction
    {
        public Exception Error { get; set; }
    }
}
