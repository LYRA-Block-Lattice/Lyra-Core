using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public class ServiceBlockAuthState : AuthState
    {
        private List<string> _allVoters;
        public ServiceBlockAuthState(List<string> AllVoters, bool haveWaiter = false) : base (haveWaiter)
        {
            _allVoters = AllVoters;
        }

        public override int WinNumber => LyraGlobal.GetMajority(_allVoters.Count);

        protected override bool CheckSenderValid(string from)
        {
            return _allVoters.Contains(from);
        }
    }
}
