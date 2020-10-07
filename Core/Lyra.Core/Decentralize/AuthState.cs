using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
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
    public enum ConsensusResult { Uncertain, Yea, Nay }
    public class ConsensusState
    {
        public DateTime Created { get; set; } = DateTime.Now;
    }
    public class AuthState : ConsensusState
    {
        // for debug profiling only
        public DateTime T1 { get; set; }
        public DateTime T2 { get; set; }
        public DateTime T3 { get; set; }
        public DateTime T4 { get; set; }
        public DateTime T5 { get; set; }
        // end

        public AuthorizedMsg LocalResult { get; set; }

        public AuthorizingMsg InputMsg { get; set; }
        public ConcurrentBag<AuthorizedMsg> OutputMsgs { get; set; }
        public ConcurrentBag<AuthorizerCommitMsg> CommitMsgs { get; set; }

        public SemaphoreSlim Semaphore { get; }
        public EventWaitHandle Done { get; set; }
        public bool Saving { get; set; }

        public ConsensusResult PrepareConsensus => GetPrepareConsensusSuccess();

        public ConsensusResult CommitConsensus => GetCommitConsensusSuccess();

        public virtual int WinNumber
        {
            get
            {
                if(_validNodes == null)
                {
                    return ProtocolSettings.Default.StandbyValidators.Length;
                }
                var minCount = LyraGlobal.GetMajority(_validNodes.Count());
                if (minCount < ProtocolSettings.Default.StandbyValidators.Length)
                    return ProtocolSettings.Default.StandbyValidators.Length;
                else
                    return minCount;
            }
        }


        ILogger _log;

        private IList<string> _validNodes;

        public AuthState(bool haveWaiter = false)
        {
            _log = new SimpleLogger("AuthState").Logger;

            Created = DateTime.Now;

            OutputMsgs = new ConcurrentBag<AuthorizedMsg>();
            CommitMsgs = new ConcurrentBag<AuthorizerCommitMsg>();

            Semaphore = new SemaphoreSlim(1, 1);
            if(haveWaiter)
                Done = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void SetView(IList<string> validNodes)
        {
            _validNodes = validNodes;
        }

        public virtual bool CheckSenderValid(string from)
        {
            if (_validNodes != null && !_validNodes.Any(a => a == from))
                return false;
            else
                return true;
        }

        public bool AddAuthResult(AuthorizedMsg msg)
        {
            // check repeated message
            if (OutputMsgs.ToList().Any(a => a.From == msg.From))
                return false;

            // only when success there is AuthSign
            if (msg.Result == APIResultCodes.Success && msg.AuthSign != null)
            {
                if (msg.From != msg.AuthSign.Key)
                    return false;

                // verify signature
                if (!Signatures.VerifyAccountSignature(InputMsg.Block.Hash, msg.AuthSign.Key, msg.AuthSign.Signature))
                {
                    _log.LogError($"AuthorizedMsg from {msg.From.Shorten()} for block {InputMsg.Block.Hash.Shorten()} verification failed.");
                    return false;
                }
            }

            // check for valid validators
            if (!CheckSenderValid(msg.From))
                return false;

            OutputMsgs.Add(msg);
            return true;
        }

        public bool AddCommitedResult(AuthorizerCommitMsg msg)
        {
            //_log.LogInformation($"Commit msg from: {msg.From.Shorten()}");
            // check repeated message
            if (CommitMsgs.ToList().Any(a => a.From == msg.From))
                return false;

            // check network state
            // !! only accept from svcBlock ( or associated view )
            // check for valid validators
            if (!CheckSenderValid(msg.From))
                return false;

            CommitMsgs.Add(msg);

            return true;
        }

        private ConsensusResult CheckAuthorizedResults()
        {
            var AuthMsgList = OutputMsgs.ToList();
            var ok = AuthMsgList.Count(a => a.IsSuccess);
            if (ok >= WinNumber)
                return ConsensusResult.Yea;

            var notok = AuthMsgList.Count(a => !a.IsSuccess);
            if (notok >= WinNumber)
                return ConsensusResult.Nay;

            return ConsensusResult.Uncertain;
        }

        private ConsensusResult CheckCommitedResults()
        {
            var CommitMsgList = CommitMsgs.ToList();
            var ok = CommitMsgList.Count(a => a.Consensus == ConsensusResult.Yea);
            if (ok >= WinNumber)
                return ConsensusResult.Yea;

            var notok = CommitMsgList.Count(a => a.Consensus == ConsensusResult.Nay);
            if (notok >= WinNumber)
                return ConsensusResult.Nay;

            return ConsensusResult.Uncertain;
            //var CommitMsgList = CommitMsgs.ToList();
            //if (CommitMsgList.Count(a => a.Consensus == ConsensusResult.Yay) >= WinNumber
            //    || CommitMsgList.Count(a => a.Consensus == ConsensusResult.Nay) >= WinNumber)
            //{
            //    _log.LogInformation($"Committed: {ConsensusUIndex}/{InputMsg.Block.Index} Yay: {CommitMsgs.Count(a => a.Consensus == ConsensusResult.Yay)} of {CommitMsgs.Select(a => a.From.Shorten()).Aggregate((x, y) => x + ", " + y)}");
            //    return true;
            //    //Settled = true;
            //    //Done.Set();
            //}
            //else
            //    return false;
        }

        private ConsensusResult GetPrepareConsensusSuccess()
        {
            //if (ConsensusUIndex < 0)
            //    return ConsensusResult.Uncertain;

            var authResult = CheckAuthorizedResults();

            if (authResult == ConsensusResult.Yea)
                return ConsensusResult.Yea;

            if (authResult == ConsensusResult.Nay)
                return ConsensusResult.Nay;

            return ConsensusResult.Uncertain;
        }

        private ConsensusResult GetCommitConsensusSuccess()
        {
            var commitResult = CheckCommitedResults();

            if (commitResult == ConsensusResult.Yea)
                return ConsensusResult.Yea;

            if (commitResult == ConsensusResult.Nay)
                return ConsensusResult.Nay;

            return ConsensusResult.Uncertain;
        }

        public void Close()
        {
            if (Semaphore != null)
                Semaphore.Dispose();
            if (Done != null)
                Done.Dispose();
        }
    }
}
