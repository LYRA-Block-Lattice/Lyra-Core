using Lyra.Core.API;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public delegate void LeaderSelectedHandler(ViewChangeHandler sender, string NewLeader, int Votes);
    public class ViewChangeHandler : ConsensusHandlerBase
    {
        private long _viewId = 0;
        public bool IsLeaderSelected { get; private set; }
        public string NewLeader { get; private set; }
        public int NewLeaderVotes { get; private set; }
        private LeaderSelectedHandler _leaderSelected;

        public ConcurrentBag<ViewChangeRequestMessage> _reqMsgs { get; set; }
        public ConcurrentDictionary<string, ViewChangeReplyMessage> _replyMsgs { get; set; }
        public ConcurrentDictionary<string, ViewChangeCommitMessage> _commitMsgs { get; set; }

        private int QualifiedNodeCount =>
            _context.Board.AllNodes.Count(a => a.Votes >= LyraGlobal.MinimalAuthorizerBalance) > LyraGlobal.MAXIMUM_AUTHORIZERS ?
            LyraGlobal.MAXIMUM_AUTHORIZERS :
            _context.Board.AllNodes.Count(a => a.Votes >= LyraGlobal.MinimalAuthorizerBalance);

        public ViewChangeHandler(ConsensusService context, LeaderSelectedHandler leaderSelected) : base(context)
        {
            _reqMsgs = new ConcurrentBag<ViewChangeRequestMessage>();
            _replyMsgs = new ConcurrentDictionary<string, ViewChangeReplyMessage>();
            _commitMsgs = new ConcurrentDictionary<string, ViewChangeCommitMessage>();

            _leaderSelected = leaderSelected;
        }

        // debug only. should remove after
        public override bool CheckTimeout()
        {
            if (DateTime.Now - _dtStart > TimeSpan.FromSeconds(10))
            {
                return true;
            }
            else
                return false;
        }
        protected override bool IsStateCreated()
        {
            return true;
        }

        public void Reset()
        {
            _log.LogWarning("Reset");
            _viewId = 0;

            _reqMsgs.Clear();
            _replyMsgs.Clear();
            _commitMsgs.Clear();
        }

        internal async Task ProcessMessage(ViewChangeMessage vcm)
        {
            if(_viewId == 0)
            {
                // other node request to change view
                var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();
                _viewId = lastSb.Height + 1;
            }

            _log.LogInformation($"ViewChangeHandler ProcessMessage From {vcm.From.Shorten()} with ViewID {vcm.ViewID} My ViewID {_viewId} ");

            if (_viewId == vcm.ViewID && GetIsMessageLegal(vcm))      // not the next one
            {
                switch (vcm)
                {
                    case ViewChangeRequestMessage req:
                        await CheckRequestAsync(req);
                        break;
                    case ViewChangeReplyMessage reply:
                        CheckReply(reply);
                        break;
                    case ViewChangeCommitMessage commit:
                        CheckCommit(commit);
                        break;
                    default:
                        _log.LogWarning("Should not happen.");
                        break;
                }
            }
        }

        private bool GetIsMessageLegal(ViewChangeMessage vcm)
        {
            if (_context.Board.AllNodes.OrderByDescending(a => a.Votes).Take(QualifiedNodeCount).Any(x => x.AccountID == vcm.From))
            {
                return true;
            }
            else
            {
                _log.LogInformation($"ViewChangeMessage Not Legal from {vcm.From.Shorten()}");
                return false;
            }
        }

        private void CheckAllStats()
        {
            _log.LogInformation($"CheckAllStats Req: {_reqMsgs.Count} Reply {_replyMsgs.Count} Commit {_commitMsgs.Count} Votes {_commitMsgs.Count}/{QualifiedNodeCount} ");
            // request
            if (_reqMsgs.Count >= LyraGlobal.GetMajority(QualifiedNodeCount))
            {
                var reply = new ViewChangeReplyMessage
                {
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    ViewID = _viewId,
                    Result = Blocks.APIResultCodes.Success,
                    Candidate = _reqMsgs.OrderBy(a => a.requestSignature).First().From
                };

                _context.Send2P2pNetwork(reply);
            }

            // reply
            // only if we have enough reply
            var qr = from rep in _replyMsgs.Values
                     where rep.Result == Blocks.APIResultCodes.Success
                     group rep by rep.Candidate into g
                     select new { Candidate = g.Key, Count = g.Count() };

            var candidateQR = qr.FirstOrDefault();

            if (candidateQR?.Count >= LyraGlobal.GetMajority(QualifiedNodeCount))
            {
                var commit = new ViewChangeCommitMessage
                {
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    ViewID = _viewId,
                    Candidate = candidateQR.Candidate,
                    Consensus = ConsensusResult.Yea
                };

                _context.Send2P2pNetwork(commit);
            }

            // commit
            var q = from rep in _commitMsgs.Values
                    group rep by rep.Candidate into g
                    select new { Candidate = g.Key, Count = g.Count() };

            var candidate = q.FirstOrDefault();
            if (candidate?.Count >= LyraGlobal.GetMajority(QualifiedNodeCount))
            {
                NewLeader = candidate.Candidate;
                IsLeaderSelected = true;
                NewLeaderVotes = candidate.Count;
                _leaderSelected(this, candidate.Candidate, candidate.Count);
            }
        }

        private void CheckCommit(ViewChangeCommitMessage vcm)
        {
            _log.LogInformation($"CheckCommit for view {vcm.ViewID} with Candidate {vcm.Candidate} of {_commitMsgs.Count}/{QualifiedNodeCount}");

            _commitMsgs.AddOrUpdate(vcm.From, vcm, (key, oldValue) => vcm);

            CheckAllStats();
        }

        private void CheckReply(ViewChangeReplyMessage reply)
        {
            _log.LogInformation($"CheckReply for view {reply.ViewID} with Candidate {reply.Candidate} of {_replyMsgs.Count}/{QualifiedNodeCount}");

            if (_replyMsgs.ContainsKey(reply.From))
            {
                _replyMsgs[reply.From] = reply;
            }
            else
            {
                _replyMsgs.TryAdd(reply.From, reply);
            }

            CheckAllStats();
        }

        private async Task CheckRequestAsync(ViewChangeRequestMessage req)
        {
            _log.LogInformation($"CheckRequestAsync from {req.From.Shorten()} for view {req.ViewID} Signature {req.requestSignature.Shorten()}");

            if (!_reqMsgs.Any(a => a.From == req.From))
            {
                var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();
                var lastCons = await _context.GetDagSystem().Storage.GetLastConsolidationBlockAsync();

                if (Signatures.VerifyAccountSignature($"{lastSb.Hash}|{lastCons.Hash}", req.From, req.requestSignature))
                    _reqMsgs.Add(req);
                else
                    _log.LogWarning($"ViewChangeRequest signature verification failed from {req.From.Shorten()}");
            }

            CheckAllStats();
        }

        internal async Task BeginChangeViewAsync()
        {
            _log.LogInformation($"BeginChangeViewAsync, need {LyraGlobal.GetMajority(QualifiedNodeCount)} vote of {QualifiedNodeCount}");

            var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();
            var lastCons = await _context.GetDagSystem().Storage.GetLastConsolidationBlockAsync();

            if (_viewId == 0)
            {
                _viewId = lastSb.Height + 1;
            }
            else if(_viewId != lastSb.Height + 1)
            {
                _log.LogError($"BeginChangeViewAsync with different viewID!!!");
                return;
            }

            IsLeaderSelected = false;
            NewLeader = null;
            NewLeaderVotes = 0;            

            var req = new ViewChangeRequestMessage
            {
                From = _context.GetDagSystem().PosWallet.AccountId,
                ViewID = lastSb.Height + 1,
                prevViewID = lastSb.Height,
                requestSignature = Signatures.GetSignature(_context.GetDagSystem().PosWallet.PrivateKey,
                    $"{lastSb.Hash}|{lastCons.Hash}", _context.GetDagSystem().PosWallet.AccountId),
            };

            _context.Send2P2pNetwork(req);
            await CheckRequestAsync(req);
        }
    }


}
