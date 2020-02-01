using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using Neo;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Lyra.Core.Decentralize
{
    public enum ConsensusResult { Uncertain, Yay, Nay }
    public class AuthState
    {
        public DateTime Created { get; private set; }

        public DateTime T1 { get; set; }
        public DateTime T2 { get; set; }
        public DateTime T3 { get; set; }
        public DateTime T4 { get; set; }

        public DateTime T5 { get; set; }

        public string HashOfFirstBlock { get; set; }
        public AuthorizingMsg InputMsg { get; set; }
        public ConcurrentBag<AuthorizedMsg> OutputMsgs { get; set; }
        public ConcurrentBag<AuthorizerCommitMsg> CommitMsgs { get; set; }

        public SemaphoreSlim Semaphore { get; }
        public EventWaitHandle Done { get; }
        public bool Settled { get; set; }
        public bool Saving { get; set; }

        private ConsensusResult _consensusResult;

        public ConsensusResult Consensus {
            get
            {
                if (!Settled)
                {
                    _consensusResult = GetConsensusSuccess();
                    Settled = true;                    
                }
                return _consensusResult;
            }        
        }

        ILogger _log;

        public AuthState()
        {
            _log = new SimpleLogger("AuthState").Logger;

            Created = DateTime.Now;

            OutputMsgs = new ConcurrentBag<AuthorizedMsg>();
            CommitMsgs = new ConcurrentBag<AuthorizerCommitMsg>();

            Semaphore = new SemaphoreSlim(1, 1);
            Done = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public bool AddAuthResult(AuthorizedMsg msg)
        {
            // check repeated message
            if (OutputMsgs.ToList().Any(a => a.From == msg.From))
                return false;

            OutputMsgs.Add(msg);
            return true;
        }

        public bool AddCommitedResult(AuthorizerCommitMsg msg)
        {
            //_log.LogInformation($"Commit msg from: {msg.From}");
            // check repeated message
            if (CommitMsgs.ToList().Any(a => a.From == msg.From))
                return false;

            CommitMsgs.Add(msg);

            return true;
        }

        private ConsensusResult CheckAuthorizedResults()
        {
            var AuthMsgList = OutputMsgs.ToList();
            var ok = AuthMsgList.Count(a => a.IsSuccess);
            if (ok >= ProtocolSettings.Default.ConsensusWinNumber)
                return ConsensusResult.Yay;

            var notok = AuthMsgList.Count(a => !a.IsSuccess);
            if (notok >= ProtocolSettings.Default.ConsensusWinNumber)
                return ConsensusResult.Nay;

            return ConsensusResult.Uncertain;
        }

        private ConsensusResult CheckCommitedResults()
        {
            var CommitMsgList = CommitMsgs.ToList();
            var ok = CommitMsgList.Count(a => a.Consensus == ConsensusResult.Yay);
            if (ok >= ProtocolSettings.Default.ConsensusWinNumber)
                return ConsensusResult.Yay;

            var notok = CommitMsgList.Count(a => a.Consensus == ConsensusResult.Nay);
            if (notok >= ProtocolSettings.Default.ConsensusWinNumber)
                return ConsensusResult.Nay;

            return ConsensusResult.Uncertain;
            //var CommitMsgList = CommitMsgs.ToList();
            //if (CommitMsgList.Count(a => a.Consensus == ConsensusResult.Yay) >= ProtocolSettings.Default.ConsensusWinNumber
            //    || CommitMsgList.Count(a => a.Consensus == ConsensusResult.Nay) >= ProtocolSettings.Default.ConsensusWinNumber)
            //{
            //    _log.LogInformation($"Committed: {ConsensusUIndex}/{InputMsg.Block.Index} Yay: {CommitMsgs.Count(a => a.Consensus == ConsensusResult.Yay)} of {CommitMsgs.Select(a => a.From.Shorten()).Aggregate((x, y) => x + ", " + y)}");
            //    return true;
            //    //Settled = true;
            //    //Done.Set();
            //}
            //else
            //    return false;
        }

        private ConsensusResult GetConsensusSuccess()
        {
            var authResult = CheckAuthorizedResults();
            var commitResult = CheckCommitedResults();

            if (authResult == ConsensusResult.Yay || commitResult == ConsensusResult.Yay)
                return ConsensusResult.Yay;

            if (authResult == ConsensusResult.Nay || commitResult == ConsensusResult.Nay)
                return ConsensusResult.Yay;

            return ConsensusResult.Uncertain;
        }


        public long ConsensusUIndex
        {
            get
            {
                // implicty GetIsAuthoringSuccess true
                // get from seed node. so we must keep seeds synced in perfect state.
                var outputMsgsList = OutputMsgs.ToList();

                for (int i = 0; i < ProtocolSettings.Default.StandbyValidators.Length; i++)
                {
                    var authenSeed = outputMsgsList.FirstOrDefault(a => a.From == ProtocolSettings.Default.StandbyValidators[i]);
                    if (authenSeed != null && authenSeed.BlockUIndex != 0)
                    {
                        return authenSeed.BlockUIndex;
                    }
                }

                var consensusedSeed = outputMsgsList.GroupBy(a => a.BlockUIndex, a => a.From, (ndx, addr) => new { UIndex = ndx, Froms = addr.ToList() })
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
