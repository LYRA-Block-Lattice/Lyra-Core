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
    public delegate void LeaderSelectedHandler(ViewChangeHandler sender, string NewLeader, int Votes, List<string> Voters);
    
    public class ViewChangeHandler : ConsensusHandlerBase
    {
        private long _minValidViewId;
        private LeaderSelectedHandler _leaderSelected;

        private class View
        {
            public ConsensusService _context;
            public DateTime _dtStarted;

            public long _viewId;
            public bool _selectedSuccess = false;

            public List<string> _qualifiedVoters;
            public string _nextLeader;

            public ConcurrentBag<ViewChangeRequestMessage> _reqMsgs { get; set; }
            public ConcurrentDictionary<string, ViewChangeReplyMessage> _replyMsgs { get; set; }
            public ConcurrentDictionary<string, ViewChangeCommitMessage> _commitMsgs { get; set; }

            public View(ConsensusService context, long viewId)
            {
                _context = context;
                _viewId = viewId;
                _dtStarted = DateTime.Now;

                _reqMsgs = new ConcurrentBag<ViewChangeRequestMessage>();
                _replyMsgs = new ConcurrentDictionary<string, ViewChangeReplyMessage>();
                _commitMsgs = new ConcurrentDictionary<string, ViewChangeCommitMessage>();
            }
            public int QualifiedNodeCount
            {
                get
                {
                    var allNodes = _context.Board.ActiveNodes.ToList();
                    var count = allNodes.Count(a => a?.Votes >= LyraGlobal.MinimalAuthorizerBalance);
                    if (count > LyraGlobal.MAXIMUM_VOTER_NODES)
                    {
                        return LyraGlobal.MAXIMUM_VOTER_NODES;
                    }
                    else if (count < LyraGlobal.MINIMUM_AUTHORIZERS)
                    {
                        return LyraGlobal.MINIMUM_AUTHORIZERS;
                    }
                    else
                    {
                        return count;
                    }
                }
            }

            public bool CheckTimeout()
            {
                if (_dtStarted != DateTime.MinValue && DateTime.Now - _dtStarted > TimeSpan.FromSeconds(10))
                {
                    return true;
                }
                else
                    return false;
            }

            public void Reset()
            {
                //_viewId = 0;  // no change of view id
                _dtStarted = DateTime.MinValue;
                _selectedSuccess = false;

                _reqMsgs.Clear();
                _replyMsgs.Clear();
                _commitMsgs.Clear();

                _qualifiedVoters.Clear();
                _qualifiedVoters = null;
            }
        }

        Dictionary<long, View> _views;

        public ViewChangeHandler(ConsensusService context, LeaderSelectedHandler leaderSelected) : base(context)
        {
            _views = new Dictionary<long, View>();

            _leaderSelected = leaderSelected;
            _minValidViewId = 2;

            _dtStart = DateTime.MinValue;
        }

        // debug only. should remove after
        public override bool CheckTimeout()
        {
            foreach(var v in _views.Values.ToList())
            {
                if(v.CheckTimeout())
                {
                    v.Reset();
                }
            }
            return false;
        }

        public void Reset()
        {

        }
        protected override bool IsStateCreated()
        {
            return true;
        }

        private View GetView(long viewId)
        {
            if (_views.ContainsKey(viewId))
            {
                return _views[viewId];
            }
            else
            {
                var view = new View(_context, viewId);
                _views.Add(viewId, view);
                return view;
            }
        }

        internal async Task ProcessMessage(ViewChangeMessage vcm)
        {
            _log.LogInformation($"ProcessMessage type: {vcm.MsgType} from: {vcm.From.Shorten()}");
            if (vcm.ViewID < _minValidViewId)
            {
                _log.LogInformation($"ProcessMessage: view ID smaller {vcm.ViewID} than {_minValidViewId}");
                return;
            }

            View view = GetView(vcm.ViewID);
            if (view == null)
                return;

            if (view._selectedSuccess)
                return;

            // I didn't see u I don't trust your vote
            if (!_context.Board.ActiveNodes.Any(a => a.AccountID == vcm.From) ||
                DateTime.Now - _context.Board.ActiveNodes.First(a => a.AccountID == vcm.From).LastActive > TimeSpan.FromMinutes(3))
                return;

            if(view._qualifiedVoters == null)
                await LookforVotersAsync(view);
            //_log.LogInformation($"ViewChangeHandler ProcessMessage From {vcm.From.Shorten()} with ViewID {vcm.ViewID} My ViewID {_viewId} ");

            if (view._qualifiedVoters.Contains(vcm.From))      // not the next one
            {
                switch (vcm)
                {
                    case ViewChangeRequestMessage req:
                        await CheckRequestAsync(view, req);
                        break;
                    case ViewChangeReplyMessage reply:
                        CheckReply(view, reply);
                        break;
                    case ViewChangeCommitMessage commit:
                        CheckCommit(view, commit);
                        break;
                    default:
                        _log.LogWarning("Should not happen.");
                        break;
                }
            }
        }

        private void CheckAllStats(View view)
        {
            _log.LogInformation($"CheckAllStats Req: {view._reqMsgs.Count} Reply {view._replyMsgs.Count} Commit {view._commitMsgs.Count} Votes {view._commitMsgs.Count}/{view._qualifiedVoters.Count} ");
            // request
            if (view._reqMsgs.Count >= LyraGlobal.GetMajority(view._qualifiedVoters.Count))
            {
                var reply = new ViewChangeReplyMessage
                {
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    ViewID = view._viewId,
                    Result = Blocks.APIResultCodes.Success,
                    Candidate = view._nextLeader
                };

                _context.Send2P2pNetwork(reply);
                CheckReply(view, reply);
            }

            // reply
            // only if we have enough reply
            var qr = from rep in view._replyMsgs.Values
                     where rep.Result == Blocks.APIResultCodes.Success
                     group rep by rep.Candidate into g
                     select new { Candidate = g.Key, Count = g.Count() };

            var candidateQR = qr.FirstOrDefault();

            if (candidateQR?.Count >= LyraGlobal.GetMajority(view._qualifiedVoters.Count))
            {
                var commit = new ViewChangeCommitMessage
                {
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    ViewID = view._viewId,
                    Candidate = candidateQR.Candidate,
                    Consensus = ConsensusResult.Yea
                };

                _context.Send2P2pNetwork(commit);
                CheckCommit(view, commit);
            }

            // commit
            var q = from rep in view._commitMsgs.Values
                    group rep by rep.Candidate into g
                    select new { Candidate = g.Key, Count = g.Count() };

            var candidate = q.FirstOrDefault();
            if (candidate?.Count >= LyraGlobal.GetMajority(view._qualifiedVoters.Count))
            {
                view._selectedSuccess = true;
                _minValidViewId = view._viewId + 1;
                _leaderSelected(this, candidate.Candidate, candidate.Count, view._qualifiedVoters);
            }
        }

        private void CheckCommit(View view, ViewChangeCommitMessage vcm)
        {
            if(!view._commitMsgs.ContainsKey(vcm.From))
            {
                view._commitMsgs.AddOrUpdate(vcm.From, vcm, (key, oldValue) => vcm);

                _log.LogInformation($"CheckCommit from {vcm.From.Shorten()} for view {vcm.ViewID} with Candidate {vcm.Candidate.Shorten()} of {view._commitMsgs.Count}/{view._qualifiedVoters.Count}");

                CheckAllStats(view);
            }
        }

        private void CheckReply(View view, ViewChangeReplyMessage reply)
        {
            _log.LogInformation($"CheckReply for view {reply.ViewID} with Candidate {reply.Candidate.Shorten()} of {view._replyMsgs.Count}/{view._qualifiedVoters.Count}");

            if(reply.Result == Blocks.APIResultCodes.Success)
            {
                if (view._replyMsgs.ContainsKey(reply.From))
                {
                    if (view._replyMsgs[reply.From].Candidate != reply.Candidate)
                    {
                        view._replyMsgs[reply.From] = reply;
                        CheckAllStats(view);
                    }
                }
                else
                {
                    view._replyMsgs.TryAdd(reply.From, reply);
                    CheckAllStats(view);
                }
            }     
        }

        private async Task CheckRequestAsync(View view, ViewChangeRequestMessage req)
        {
            _log.LogInformation($"CheckRequestAsync from {req.From.Shorten()} for view {req.ViewID} Signature {req.requestSignature.Shorten()}");

            if (!view._reqMsgs.Any(a => a.From == req.From))
            {
                var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();
                var lastCons = await _context.GetDagSystem().Storage.GetLastConsolidationBlockAsync();

                if (Signatures.VerifyAccountSignature($"{lastSb.Hash}|{lastCons.Hash}", req.From, req.requestSignature))
                {
                    view._reqMsgs.Add(req);
                    CheckAllStats(view);
                }                    
                else
                    _log.LogWarning($"ViewChangeRequest signature verification failed from {req.From.Shorten()}");
            }            
        }

        internal async Task BeginChangeViewAsync()
        {
            _log.LogInformation($"BeginChangeViewAsync");

            var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();

            if(lastSb == null)
            {
                // genesis?
                return;
            }

            var lastCons = await _context.GetDagSystem().Storage.GetLastConsolidationBlockAsync();

            var view = GetView(lastSb.Height + 1);

            await LookforVotersAsync(view);

            var req = new ViewChangeRequestMessage
            {
                From = _context.GetDagSystem().PosWallet.AccountId,
                ViewID = lastSb.Height + 1,
                prevViewID = lastSb.Height,
                requestSignature = Signatures.GetSignature(_context.GetDagSystem().PosWallet.PrivateKey,
                    $"{lastSb.Hash}|{lastCons.Hash}", _context.GetDagSystem().PosWallet.AccountId),
            };

            _context.Send2P2pNetwork(req);
            await CheckRequestAsync(view, req);
        }

        private async Task LookforVotersAsync(View view)
        {
            var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();

            // setup the voters list
            _context.RefreshAllNodesVotes();
            view._qualifiedVoters = _context.Board.ActiveNodes
                .OrderByDescending(a => a.Votes)
                .Take(view.QualifiedNodeCount)
                .Select(a => a.AccountID)
                .ToList();
            view._qualifiedVoters.Sort();

            // the new leader:
            // 1, not the previous one;
            // 2, viewid mod [voters count], index of _qualifiedVoters.
            // 
            var leaderIndex = (int)(view._viewId % view._qualifiedVoters.Count);
            while (view._qualifiedVoters[leaderIndex] == lastSb.Leader)
            {
                leaderIndex++;
                if (leaderIndex >= view._qualifiedVoters.Count)
                    leaderIndex = 0;
            }
            view._nextLeader = view._qualifiedVoters[leaderIndex];
            _log.LogInformation($"LookforVotersAsync, next leader will be {view._nextLeader}");
        }
    }


}
