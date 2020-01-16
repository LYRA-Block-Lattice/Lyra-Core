using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
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

        ILogger _log;

        public AuthState()
        {
            _log = new SimpleLogger("AuthState").Logger;

            Created = DateTime.Now;

            OutputMsgs = new List<AuthorizedMsg>();
            CommitMsgs = new List<AuthorizerCommitMsg>();

            Done = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void AddAuthResult(AuthorizedMsg msg)
        {
            // check repeated message
            if (OutputMsgs.ToList().Any(a => a.From == msg.From))
                return;

            OutputMsgs.Add(msg);
        }

        public void AddCommitedResult(AuthorizerCommitMsg msg)
        {
            // check repeated message
            if (CommitMsgs.ToList().Any(a => a.From == msg.From))
                return;

            CommitMsgs.Add(msg);
            if (CommitMsgs.Count() >= ProtocolSettings.Default.ConsensusWinNumber)
            {
                _log.LogInformation($"Committed: {ConsensusUIndex}/{InputMsg.Block.Index} Yay: {CommitMsgs.Count} of {CommitMsgs.Select(a => a.From.Shorten()).Aggregate((x, y) => x + ", " + y)}");
                Settled = true;
                Done.Set();
            }                
        }

        public bool GetIsAuthoringSuccess(BillBoard billBoard)
        {
            if (billBoard == null)
                throw new Exception("The BillBoard mustn't be null!");

            // wait for a proper UID
            if (!OutputMsgs.ToList().Any(a => a.From == ProtocolSettings.Default.StandbyValidators[0]))
            {
                return false;
            }                

            var selectedNodes = billBoard.AllNodes.Values.Where(a => a.AbleToAuthorize).OrderByDescending(b => b.Balance).Take(ProtocolSettings.Default.ConsensusTotalNumber);

            var q = from m in OutputMsgs
                    where m.IsSuccess && selectedNodes.Any(a => a.AccountID == m.From)
                    select m;

            IsConsensusSuccess = q.Count() >= ProtocolSettings.Default.ConsensusWinNumber;

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
                if (consensusedSeed.Froms.Count >= ProtocolSettings.Default.ConsensusWinNumber)
                {
                    return consensusedSeed.UIndex;
                }

                // out of lucky??? we should halt and switch to emgergency state
                throw new Exception("Can't get UIndex");
            }
        }
    }
}
