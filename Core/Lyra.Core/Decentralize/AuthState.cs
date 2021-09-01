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
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public enum ConsensusResult { Uncertain, Yea, Nay }
    public class ConsensusState
    {
        public DateTime Created { get; set; } = DateTime.Now;
    }
    public delegate void SuccessConsensusHandler(Block block, ConsensusResult? result, bool localOk);
    public class AuthState : ConsensusState, IDisposable
    {
        private bool _commited = false;
        public bool IsCommited => _commited;
        // To detect redundant calls
        private bool _disposed = false;

        private bool _saved = false;
        public bool IsSaved => _saved;
        public void SetSaved()
        {
            _saved = true;
        }
        private bool _closed = false;
        public bool IsClosed => _closed;
        public event SuccessConsensusHandler OnConsensusSuccess;

        // for debug profiling only
        public DateTime T1 { get; set; }
        public DateTime T2 { get; set; }
        public DateTime T3 { get; set; }
        public DateTime T4 { get; set; }
        public DateTime T5 { get; set; }
        // end

        AuthorizedMsg _localResult;
        public AuthorizedMsg LocalResult
        {
            get
            {
                return _localResult;
            }
            set
            {
                _localResult = value;
                OutputMsgs.Add(value);
            }
        }

        public AuthorizingMsg InputMsg { get; set; }

        /// for service block or consolidation block, form non-leader, will be false.
        public bool IsSourceValid { get; set; }
        public ConcurrentBag<AuthorizedMsg> OutputMsgs { get; set; }
        public ConcurrentBag<AuthorizerCommitMsg> CommitMsgs { get; set; }

        public SemaphoreSlim Semaphore { get; }
        private EventWaitHandle Done { get; set; }
        public bool Saving { get; set; }

        public ConsensusResult PrepareConsensus => GetPrepareConsensusSuccess();

        public ConsensusResult? CommitConsensus => CheckCommitedResults();

        public virtual int WinNumber
        {
            get
            {
                if (_validNodes == null)
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

        readonly ILogger _log;

        private IList<string> _validNodes;

        public AuthState()
        {
            _log = new SimpleLogger("AuthState").Logger;

            Created = DateTime.Now;

            OutputMsgs = new ConcurrentBag<AuthorizedMsg>();
            CommitMsgs = new ConcurrentBag<AuthorizerCommitMsg>();

            Semaphore = new SemaphoreSlim(1, 1);
            Done = new EventWaitHandle(false, EventResetMode.ManualReset);
        }

        public void Reset()
        {
            _localResult = null;
            OutputMsgs.Clear();
            CommitMsgs.Clear();
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

        private ConsensusResult? CheckCommitedResults()
        {
            var CommitMsgList = CommitMsgs.ToList();

            if (CommitMsgList.Count < WinNumber)
            {
                // votes not enough
                return null;
            }

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

        public APIResultCodes GetMajorErrorCode()
        {
            var q = OutputMsgs.GroupBy(a => a.Result)
                    .Select(g => new { g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Count);

            if(q.Any())
            {
                return q.First().Key;
            }
            else
            {
                return APIResultCodes.UnknownError;
            }
        }

        public async Task WaitForCloseAsync()
        {
            if (Done != null)
                await Done.AsTaskAsync();
        }

        public void Commit()
        {
            if (_commited)
                return;

            try
            {
                Done.Set();
            }
            catch { }

            try
            {
                var localResultGood = false;
                if (LocalResult == null)        // far node not receive authorizing msg but the final commit msg
                {
                    _log.LogWarning("State.LocalResult is null. wait it.");
                    localResultGood = false;
                }
                else if (CommitConsensus == ConsensusResult.Yea && LocalResult.Result == APIResultCodes.Success)
                    localResultGood = true;
                else if (CommitConsensus == ConsensusResult.Nay && LocalResult.Result != APIResultCodes.Success)
                    localResultGood = true;
                else if (CommitConsensus == null || CommitConsensus == ConsensusResult.Uncertain)
                    localResultGood = true;

                OnConsensusSuccess?.Invoke(InputMsg?.Block, CommitConsensus, localResultGood);

                _commited = true;
            }
            catch (Exception exe)
            {
                _log.LogError("Call OnConsensusSuccess: " + exe);
            }
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose() => Dispose(true);

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                // Dispose managed state (managed objects).
                Semaphore?.Dispose();
            }

            _disposed = true;
        }
    }
}
