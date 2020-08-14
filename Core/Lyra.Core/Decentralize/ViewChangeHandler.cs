using Lyra.Core.API;
using Lyra.Core.Cryptography;
using Lyra.Core.Utils;
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

        public void Finish()
        {
            _viewId++;
            _reqMsgs.Clear();
            _replyMsgs.Clear();
            _commitMsgs.Clear();
        }

        protected override bool IsStateCreated()
        {
            return true;
        }

        internal async Task ProcessMessage(ViewChangeMessage vcm)
        {
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
            return false;
        }

        private void CheckCommit(ViewChangeCommitMessage vcm)
        {
            _commitMsgs.AddOrUpdate(vcm.From, vcm, (key, oldValue) => vcm);

            var q = from rep in _commitMsgs.Values
                    group rep by rep.Candidate into g
                    select new { Candidate = g.Key, Count = g.Count() };

            var candidate = q.First();
            if (candidate.Count >= LyraGlobal.GetMojority(QualifiedNodeCount))
            {
                NewLeader = candidate.Candidate;
                IsLeaderSelected = true;
                NewLeaderVotes = candidate.Count;
                _leaderSelected(this, candidate.Candidate, candidate.Count);
            }
        }

        private void CheckReply(ViewChangeReplyMessage reply)
        {
            if (_replyMsgs.ContainsKey(reply.From))
            {
                _replyMsgs[reply.From] = reply;
            }
            else
            {
                _replyMsgs.TryAdd(reply.From, reply);
            }

            // only if we have enough reply
            var q = from rep in _replyMsgs.Values
                    where rep.Result == Blocks.APIResultCodes.Success
                    group rep by rep.Candidate into g
                    select new { Candidate = g.Key, Count = g.Count() };

            var candidate = q.First();

            if(candidate.Count >= LyraGlobal.GetMojority(QualifiedNodeCount))
            {
                var commit = new ViewChangeCommitMessage
                {
                    ViewID = _viewId,
                    Candidate = candidate.Candidate,
                    Consensus = ConsensusResult.Yea
                };

                _context.Send2P2pNetwork(commit);
            }
        }

        private async Task CheckRequestAsync(ViewChangeRequestMessage req)
        {            
            if(!_reqMsgs.Any(a => a.From == req.From))
            {
                var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();
                var lastCons = await _context.GetDagSystem().Storage.GetLastConsolidationBlockAsync();

                if (Signatures.VerifyAccountSignature($"{lastSb.Hash}|{lastCons.Hash}", req.From, req.requestSignature))
                    _reqMsgs.Add(req);
            }                

            if(_reqMsgs.Count >= LyraGlobal.GetMojority(QualifiedNodeCount))
            {
                var reply = new ViewChangeReplyMessage
                {
                    ViewID = _viewId,
                    Result = Blocks.APIResultCodes.Success,
                    Candidate = _reqMsgs.OrderBy(a => a.requestSignature).First().From
                };

                _context.Send2P2pNetwork(reply);
                CheckReply(reply);
            }
        }

        internal async Task BeginChangeViewAsync()
        {
            var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();
            var lastCons = await _context.GetDagSystem().Storage.GetLastConsolidationBlockAsync();

            _viewId = lastSb.Height + 1;

            var req = new ViewChangeRequestMessage
            {
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
