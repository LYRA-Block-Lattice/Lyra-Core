using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Lyra.Core.Decentralize
{
    public class AuthState
    {
        private int ConfirmCount = 2;
        public string HashOfFirstBlock { get; set; }
        public AuthorizingMsg InputMsg { get; set; }
        public List<AuthorizedMsg> OutputMsgs { get; set; }
        public List<AuthorizerCommitMsg> CommitMsgs { get; set; }

        public EventWaitHandle Done { get; set; }
        public bool Settled { get; set; }

        public AuthState()
        {
            OutputMsgs = new List<AuthorizedMsg>();
            CommitMsgs = new List<AuthorizerCommitMsg>();

            Done = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void AddAuthResult(AuthorizedMsg msg)
        {
            OutputMsgs.Add(msg);
        }

        public void AddCommitedResult(AuthorizerCommitMsg msg)
        {
            CommitMsgs.Add(msg);
            if (CommitMsgs.Count() >= ConfirmCount)
            {
                Settled = true;
                Done.Set();
            }                
        }

        public bool IsAuthoringSuccess => OutputMsgs.Count(a => a.IsSuccess) >= ConfirmCount;
    }
}
