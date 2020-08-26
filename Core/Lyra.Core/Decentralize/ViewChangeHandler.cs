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
        private long _viewId = 0;
        private bool _selectedSuccess = false;
        private LeaderSelectedHandler _leaderSelected;

        private List<string> _qualifiedVoters;
        private string _nextLeader;

        public ConcurrentBag<ViewChangeRequestMessage> _reqMsgs { get; set; }
        public ConcurrentDictionary<string, ViewChangeReplyMessage> _replyMsgs { get; set; }
        public ConcurrentDictionary<string, ViewChangeCommitMessage> _commitMsgs { get; set; }

        private int QualifiedNodeCount
        {
            get
            {
                var allNodes = _context.Board.ActiveNodes.ToList();
                var count = allNodes.Count(a => a?.Votes >= LyraGlobal.MinimalAuthorizerBalance);
                if (count > LyraGlobal.MAXIMUM_VOTER_NODES)
                {
                    return LyraGlobal.MAXIMUM_VOTER_NODES;
                }
                else if(count < LyraGlobal.MINIMUM_AUTHORIZERS)
                {
                    return LyraGlobal.MINIMUM_AUTHORIZERS;
                }
                else
                {
                    return count;
                }
            }
        }

        public ViewChangeHandler(ConsensusService context, LeaderSelectedHandler leaderSelected) : base(context)
        {
            _reqMsgs = new ConcurrentBag<ViewChangeRequestMessage>();
            _replyMsgs = new ConcurrentDictionary<string, ViewChangeReplyMessage>();
            _commitMsgs = new ConcurrentDictionary<string, ViewChangeCommitMessage>();

            _leaderSelected = leaderSelected;
            _dtStart = DateTime.MinValue;
        }

        // debug only. should remove after
        public override bool CheckTimeout()
        {
            if (_dtStart != DateTime.MinValue && DateTime.Now - _dtStart > TimeSpan.FromSeconds(10))
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
            _dtStart = DateTime.MinValue;
            _selectedSuccess = false;

            _reqMsgs.Clear();
            _replyMsgs.Clear();
            _commitMsgs.Clear();

            _qualifiedVoters.Clear();
            _qualifiedVoters = null;
        }

        internal async Task ProcessMessage(ViewChangeMessage vcm)
        {
            // I didn't see u I don't trust your vote
            if (!_context.Board.ActiveNodes.Any(a => a.AccountID == vcm.From) ||
                DateTime.Now - _context.Board.ActiveNodes.First(a => a.AccountID == vcm.From).LastActive > TimeSpan.FromMinutes(3))
                return;

            if (vcm.ViewID == _viewId && _selectedSuccess)
                return;

            if(_viewId == 0)
            {
                // other node request to change view
                var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();
                _viewId = lastSb.Height + 1;
                _dtStart = DateTime.Now;
            }

            if(_qualifiedVoters == null)
                await LookforVotersAsync();
            //_log.LogInformation($"ViewChangeHandler ProcessMessage From {vcm.From.Shorten()} with ViewID {vcm.ViewID} My ViewID {_viewId} ");

            if (_viewId == vcm.ViewID && _qualifiedVoters.Contains(vcm.From))      // not the next one
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

        private void CheckAllStats()
        {
            _log.LogInformation($"CheckAllStats Req: {_reqMsgs.Count} Reply {_replyMsgs.Count} Commit {_commitMsgs.Count} Votes {_commitMsgs.Count}/{_qualifiedVoters.Count} ");
            // request
            if (_reqMsgs.Count >= LyraGlobal.GetMajority(_qualifiedVoters.Count))
            {
                var reply = new ViewChangeReplyMessage
                {
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    ViewID = _viewId,
                    Result = Blocks.APIResultCodes.Success,
                    Candidate = _nextLeader
                };

                _context.Send2P2pNetwork(reply);
                CheckReply(reply);
            }

            // reply
            // only if we have enough reply
            var qr = from rep in _replyMsgs.Values
                     where rep.Result == Blocks.APIResultCodes.Success
                     group rep by rep.Candidate into g
                     select new { Candidate = g.Key, Count = g.Count() };

            var candidateQR = qr.FirstOrDefault();

            if (candidateQR?.Count >= LyraGlobal.GetMajority(_qualifiedVoters.Count))
            {
                var commit = new ViewChangeCommitMessage
                {
                    From = _context.GetDagSystem().PosWallet.AccountId,
                    ViewID = _viewId,
                    Candidate = candidateQR.Candidate,
                    Consensus = ConsensusResult.Yea
                };

                _context.Send2P2pNetwork(commit);
                CheckCommit(commit);
            }

            // commit
            var q = from rep in _commitMsgs.Values
                    group rep by rep.Candidate into g
                    select new { Candidate = g.Key, Count = g.Count() };

            var candidate = q.FirstOrDefault();
            if (candidate?.Count >= LyraGlobal.GetMajority(_qualifiedVoters.Count))
            {
                _leaderSelected(this, candidate.Candidate, candidate.Count, _qualifiedVoters);
            }
        }

        private void CheckCommit(ViewChangeCommitMessage vcm)
        {
            _log.LogInformation($"CheckCommit from {vcm.From.Shorten()} for view {vcm.ViewID} with Candidate {vcm.Candidate.Shorten()} of {_commitMsgs.Count}/{_qualifiedVoters.Count}");

            if(!_commitMsgs.ContainsKey(vcm.From))
            {
                _commitMsgs.AddOrUpdate(vcm.From, vcm, (key, oldValue) => vcm);

                CheckAllStats();
            }
        }

        private void CheckReply(ViewChangeReplyMessage reply)
        {
            _log.LogInformation($"CheckReply for view {reply.ViewID} with Candidate {reply.Candidate.Shorten()} of {_replyMsgs.Count}/{_qualifiedVoters.Count}");

            if(reply.Result == Blocks.APIResultCodes.Success)
            {
                if (_replyMsgs.ContainsKey(reply.From))
                {
                    if (_replyMsgs[reply.From].Candidate != reply.Candidate)
                    {
                        _replyMsgs[reply.From] = reply;
                        CheckAllStats();
                    }
                }
                else
                {
                    _replyMsgs.TryAdd(reply.From, reply);
                    CheckAllStats();
                }
            }     
        }

        private async Task CheckRequestAsync(ViewChangeRequestMessage req)
        {
            _log.LogInformation($"CheckRequestAsync from {req.From.Shorten()} for view {req.ViewID} Signature {req.requestSignature.Shorten()}");

            if (!_reqMsgs.Any(a => a.From == req.From))
            {
                var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();
                var lastCons = await _context.GetDagSystem().Storage.GetLastConsolidationBlockAsync();

                if (Signatures.VerifyAccountSignature($"{lastSb.Hash}|{lastCons.Hash}", req.From, req.requestSignature))
                {
                    _reqMsgs.Add(req);
                    CheckAllStats();
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

            if (_viewId == 0)
            {
                _viewId = lastSb.Height + 1;
                _dtStart = DateTime.Now;
            }
            else if(_viewId != lastSb.Height + 1)
            {
                _log.LogError($"BeginChangeViewAsync with different viewID!!!");
                return;
            }

            await LookforVotersAsync();

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

        private async Task LookforVotersAsync()
        {
            var lastSb = await _context.GetDagSystem().Storage.GetLastServiceBlockAsync();

            // setup the voters list
            _context.RefreshAllNodesVotes();
            _qualifiedVoters = _context.Board.ActiveNodes
                .OrderByDescending(a => a.Votes)
                .Take(QualifiedNodeCount)
                .Select(a => a.AccountID)
                .ToList();
            _qualifiedVoters.Sort();

            // the new leader:
            // 1, not the previous one;
            // 2, viewid mod [voters count], index of _qualifiedVoters.
            // 
            var leaderIndex = (int)(_viewId % _qualifiedVoters.Count);
            while (_qualifiedVoters[leaderIndex] == lastSb.Leader)
            {
                leaderIndex++;
                if (leaderIndex >= _qualifiedVoters.Count)
                    leaderIndex = 0;
            }
            _nextLeader = _qualifiedVoters[leaderIndex];
            _log.LogInformation($"BeginChangeViewAsync, next leader will be {_nextLeader}");
        }
    }


}
