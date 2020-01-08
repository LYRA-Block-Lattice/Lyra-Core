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
        public DateTime Created { get; private set; }

        public string HashOfFirstBlock { get; set; }
        public AuthorizingMsg InputMsg { get; set; }
        public List<AuthorizedMsg> OutputMsgs { get; set; }
        public List<AuthorizerCommitMsg> CommitMsgs { get; set; }

        public EventWaitHandle Done { get; set; }
        public bool Settled { get; set; }
        public bool Saving { get; set; }

        public bool? IsConsensusSuccess { get; private set; }

        public AuthState()
        {
            Created = DateTime.Now;

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
            if (CommitMsgs.Count() >= ProtocolSettings.Default.ConsensusNumber)
            {
                Settled = true;
                Done.Set();
            }                
        }

        public bool GetIsAuthoringSuccess(BillBoard billBoard)
        {
            if (billBoard == null)
                throw new Exception("The BillBoard mustn't be null!");

            var topNodes = billBoard.AllNodes.Values.Where(a => a.AbleToAuthorize).OrderByDescending(b => b.Balance).Take(21);
            var agreeCount = (topNodes.Count() / 3) * 2 + 1;
            if (agreeCount < ProtocolSettings.Default.ConsensusNumber)
                agreeCount = (int)ProtocolSettings.Default.ConsensusNumber;      // we must do genesis when authorizers are there

            var q = from m in OutputMsgs
                    where m.IsSuccess && billBoard.AllNodes.ContainsKey(m.From) && billBoard.AllNodes[m.From].AbleToAuthorize
                    select m;

            IsConsensusSuccess = q.Count() >= agreeCount;
            return IsConsensusSuccess == true;
        }

        public long ConsensusUIndex
        {
            get
            {
                // implicty GetIsAuthoringSuccess true
                // get from seed node. so we must keep seeds synced in perfect state.
                for (int i = 0; i < ProtocolSettings.Default.StandbyValidators.Length; i++)
                {
                    var authenSeed = OutputMsgs.FirstOrDefault(a => a.From == ProtocolSettings.Default.StandbyValidators[i]);
                    if (authenSeed != null && authenSeed.BlockUIndex != 0)
                    {
                        return authenSeed.BlockUIndex;
                    }
                }

                var consensusedSeed = OutputMsgs.GroupBy(a => a.BlockUIndex, a => a.From, (ndx, addr) => new { UIndex = ndx, Froms = addr.ToList() })
                    .OrderByDescending(b => b.Froms.Count)
                    .First();
                if (consensusedSeed.Froms.Count >= ProtocolSettings.Default.ConsensusNumber)
                {
                    return consensusedSeed.UIndex;
                }

                // out of lucky??? we should halt and switch to emgergency state
                throw new Exception("Can't get UIndex");
            }
        }
    }
}
