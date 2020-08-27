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
    public delegate void LeaderSelectedHandler(ViewChangeHandler sender, long viewId, string NewLeader, int Votes, List<string> Voters);
    
    public class ViewChangeHandler : ConsensusHandlerBase
    {
        private long _ValidViewId;
        private LeaderSelectedHandler _leaderSelected;

        private class VCReqWithTime
        {
            public ViewChangeRequestMessage msg { get; set; }
            public DateTime Time { get; } = DateTime.Now;
            public VCReqWithTime(ViewChangeRequestMessage Message)
            {
                msg = Message;
            }
        }
        private class VCReplyWithTime
        {
            public ViewChangeReplyMessage msg { get; set; }
            public DateTime Time { get; } = DateTime.Now;
            public VCReplyWithTime(ViewChangeReplyMessage Message)
            {
                msg = Message;
            }
        }
        private class VCCommitWithTime
        {
            public ViewChangeCommitMessage msg { get; set; }
            public DateTime Time { get; } = DateTime.Now;
            public VCCommitWithTime(ViewChangeCommitMessage Message)
            {
                msg = Message;
            }
        }
        private class View
        {
            DagSystem _sys;
            public ConsensusService context;
            public DateTime dtStarted;

            public long viewId;
            public bool selectedSuccess = false;

            public string nextLeader;

            public ConcurrentDictionary<string, VCReqWithTime> reqMsgs { get; set; }
            public ConcurrentDictionary<string, VCReplyWithTime> replyMsgs { get; set; }
            public ConcurrentDictionary<string, VCCommitWithTime> commitMsgs { get; set; }
            public View(DagSystem sys, ConsensusService context, long viewId)
            {
                this._sys = sys;
                this.context = context;
                this.viewId = viewId;
                dtStarted = DateTime.Now;

                reqMsgs = new ConcurrentDictionary<string, VCReqWithTime>();
                replyMsgs = new ConcurrentDictionary<string, VCReplyWithTime>();
                commitMsgs = new ConcurrentDictionary<string, VCCommitWithTime>();
            }
            public int QualifiedNodeCount
            {
                get
                {
                    var allNodes = context.Board.ActiveNodes.ToList();
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
                if (dtStarted != DateTime.MinValue && DateTime.Now - dtStarted > TimeSpan.FromSeconds(10))
                {
                    return true;
                }
                else
                    return false;
            }

            public void Reset()
            {
                //_viewId = 0;  // no change of view id
                dtStarted = DateTime.Now;
                selectedSuccess = false;

                reqMsgs.Clear();
                replyMsgs.Clear();
                commitMsgs.Clear();

                //_qualifiedVoters.Clear();
                //_qualifiedVoters = null;
            }
        }

        ConcurrentDictionary<long, View> _views;

        DagSystem _sys;
        public ViewChangeHandler(DagSystem sys, ConsensusService context, LeaderSelectedHandler leaderSelected) : base(context)
        {
            _sys = sys;
            _views = new ConcurrentDictionary<long, View>();

            _leaderSelected = leaderSelected;

            _dtStart = DateTime.MinValue;
        }

        // debug only. should remove after
        public override bool CheckTimeout()
        {
            //foreach(var v in _views.Values.ToList())
            //{
            //    if(v.CheckTimeout())
            //    {
            //        _log.LogInformation($"View Change with Id {v.viewId} timeout.");
            //        v.Reset();                    

            //        Task.Run(async () => {
            //            await LookforVotersAsync(v);
            //            await Task.Delay(2000);
            //            await BeginChangeViewAsync();
            //        });
            //    }
            //}
            return false;
        }

        public void Reset(long viewId, List<string> excludes)
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
                var view = new View(_sys, _context, viewId);
                _views.TryAdd(viewId, view);
                return view;
            }
        }

        internal async Task ProcessMessage(ViewChangeMessage vcm)
        {
            if(_context.CurrentState != BlockChainState.ViewChanging)
            {
                // only accept req when non vc
                if (vcm.MsgType != ChatMessageType.ViewChangeRequest)
                    return;
            }

            _log.LogInformation($"ProcessMessage type: {vcm.MsgType} from: {vcm.From.Shorten()}");
            if (_ValidViewId != 0 && vcm.ViewID != _ValidViewId)
            {
                _log.LogInformation($"ProcessMessage: view ID {vcm.ViewID} not valid with {_ValidViewId}");
                return;
            }

            View view = GetView(vcm.ViewID);
            if (view == null)
                return;

            if (view.selectedSuccess)
                return;

            if(vcm is ViewChangeRequestMessage req)
            {
                await CheckRequestAsync(view, req);
                return;
            }

            //_log.LogInformation($"ViewChangeHandler ProcessMessage From {vcm.From.Shorten()} with ViewID {vcm.ViewID} My ViewID {_viewId} ");

            if (_context.Board.AllVoters.Contains(vcm.From))      // not the next one
            {
                switch (vcm)
                {
                    case ViewChangeReplyMessage reply:
                        await CheckReplyAsync(view, reply);
                        break;
                    case ViewChangeCommitMessage commit:
                        await CheckCommitAsync(view, commit);
                        break;
                    default:
                        _log.LogWarning("Should not happen.");
                        break;
                }
            }
        }

        private async Task CheckAllStatsAsync(View view)
        {
            _log.LogInformation($"CheckAllStats Req: {view.reqMsgs.Count} Reply {view.replyMsgs.Count} Commit {view.commitMsgs.Count} Votes {view.commitMsgs.Count}/{_context.Board.AllVoters.Count} ");

            // remove outdated msgs
            var q1 = view.reqMsgs.Where(a => a.Value.Time < DateTime.Now.AddSeconds(-15))
                .Select(b => b.Key)
                .ToList();
            foreach (var req in q1)
                view.reqMsgs.TryRemove(req, out _);

            var q2 = view.reqMsgs.Where(a => a.Value.Time < DateTime.Now.AddSeconds(-15))
                .Select(b => b.Key)
                .ToList();
            foreach (var req in q2)
                view.replyMsgs.TryRemove(req, out _);

            var q3 = view.reqMsgs.Where(a => a.Value.Time < DateTime.Now.AddSeconds(-15))
                .Select(b => b.Key)
                .ToList();
            foreach (var req in q3)
                view.commitMsgs.TryRemove(req, out _);

            if (_context.Board.AllVoters.Count == 0)
                LookforVoters(view);

            // request
            if (view.reqMsgs.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                // the new leader:
                // 1, not the previous one;
                // 2, viewid mod [voters count], index of _qualifiedVoters.
                // 
                var leaderIndex = (int)(view.viewId % _context.Board.AllVoters.Count);

                do
                {
                    var leader = _context.Board.AllVoters[leaderIndex];
                    if (!view.reqMsgs.Values.Any(a => a.msg.From == leader))     // it is offline
                    {
                        leaderIndex = (leaderIndex + 1) % _context.Board.AllVoters.Count;
                    }
                    else
                        break;
                } while (true);

                view.nextLeader = _context.Board.AllVoters[leaderIndex];
                _log.LogInformation($"CheckAllStats, By ReqMsgs, next leader will be {view.nextLeader}");

                var reply = new ViewChangeReplyMessage
                {
                    From = _sys.PosWallet.AccountId,
                    ViewID = view.viewId,
                    Result = Blocks.APIResultCodes.Success,
                    Candidate = view.nextLeader
                };

                _context.Send2P2pNetwork(reply);
                await CheckReplyAsync(view, reply);
            }
            else if (view.reqMsgs.Count == _context.Board.AllVoters.Count - LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                LookforVoters(view);
                // also do clean of req msgs queue
                var unqualifiedReqs = view.reqMsgs.Keys.Where(a => !_context.Board.AllVoters.Contains(a));
                foreach (var unq in unqualifiedReqs)
                    view.reqMsgs.Remove(unq, out _);
            }
            else if(view.reqMsgs.Count > _context.Board.AllVoters.Count - LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                if(_context.CurrentState != BlockChainState.ViewChanging)
                {
                    // too many view change request. force into view change mode
                    await _context.GotViewChangeRequestAsync(view.viewId);
                }
            }

            // reply
            // only if we have enough reply
            var qr = from rep in view.replyMsgs.Values
                     where rep.msg.Result == Blocks.APIResultCodes.Success
                     group rep by rep.msg.Candidate into g
                     select new { Candidate = g.Key, Count = g.Count() };

            var candidateQR = qr.FirstOrDefault();

            if (candidateQR?.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                _log.LogInformation($"CheckAllStats, By ReplyMsgs, commit next leader {view.nextLeader}");

                var commit = new ViewChangeCommitMessage
                {
                    From = _sys.PosWallet.AccountId,
                    ViewID = view.viewId,
                    Candidate = candidateQR.Candidate,
                    Consensus = ConsensusResult.Yea
                };

                _context.Send2P2pNetwork(commit);
                await CheckCommitAsync(view, commit);
            }

            // commit
            var q = from rep in view.commitMsgs.Values
                    group rep by rep.msg.Candidate into g
                    select new { Candidate = g.Key, Count = g.Count() };

            var candidate = q.FirstOrDefault();
            if (candidate?.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                _log.LogInformation($"CheckAllStats, By CommitMsgs, leader selected {view.nextLeader}");

                view.selectedSuccess = true;
                _leaderSelected(this, view.viewId, candidate.Candidate, candidate.Count, _context.Board.AllVoters);
            }
        }

        private async Task CheckCommitAsync(View view, ViewChangeCommitMessage vcm)
        {
            if(!view.commitMsgs.ContainsKey(vcm.From))
            {
                var cmt = new VCCommitWithTime(vcm);
                view.commitMsgs.AddOrUpdate(vcm.From, cmt, (key, oldValue) => cmt);

                _log.LogInformation($"CheckCommit from {vcm.From.Shorten()} for view {vcm.ViewID} with Candidate {vcm.Candidate.Shorten()} of {view.commitMsgs.Count}/{LyraGlobal.GetMajority(_context.Board.AllVoters.Count)}/{_context.Board.AllVoters.Count}");

                await CheckAllStatsAsync(view);
            }
        }

        private async Task CheckReplyAsync(View view, ViewChangeReplyMessage reply)
        {
            //_log.LogInformation($"CheckReply for view {reply.ViewID} with Candidate {reply.Candidate.Shorten()} of {view._replyMsgs.Count}/{view._qualifiedVoters.Count}");

            if(reply.Result == Blocks.APIResultCodes.Success)
            {
                if (view.replyMsgs.ContainsKey(reply.From))
                {
                    if (view.replyMsgs[reply.From].msg.Candidate != reply.Candidate)
                    {
                        view.replyMsgs[reply.From] = new VCReplyWithTime(reply);
                        await CheckAllStatsAsync(view);
                    }
                }
                else
                {
                    view.replyMsgs.TryAdd(reply.From, new VCReplyWithTime(reply));
                    await CheckAllStatsAsync(view);
                }
            }     
        }

        private async Task CheckRequestAsync(View view, ViewChangeRequestMessage req)
        {
            //_log.LogInformation($"CheckRequestAsync from {req.From.Shorten()} for view {req.ViewID} Signature {req.requestSignature.Shorten()}");

            if (!view.reqMsgs.Values.Any(a => a.msg.From == req.From))
            {
                var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
                var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();

                if (Signatures.VerifyAccountSignature($"{lastSb.Hash}|{lastCons.Hash}", req.From, req.requestSignature))
                {
                    view.reqMsgs.TryAdd(req.From, new VCReqWithTime(req));
                    await CheckAllStatsAsync(view);
                }                    
                else
                    _log.LogWarning($"ViewChangeRequest signature verification failed from {req.From.Shorten()}");
            }            
        }

        internal async Task BeginChangeViewAsync()
        {
            _log.LogInformation($"Begin Change View.");

            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();

            if(lastSb == null)
            {
                // genesis?
                _log.LogCritical($"BeginChangeViewAsync has null service block. should not happend. error.");
                return;
            }

            var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();

            _ValidViewId = lastSb.Height + 1;

            foreach(var v in _views.ToList())
            {
                if (v.Key != _ValidViewId)
                    _views.TryRemove(v.Key, out _);
            }

            var view = GetView(_ValidViewId);

            LookforVoters(view);

            var req = new ViewChangeRequestMessage
            {
                From = _sys.PosWallet.AccountId,
                ViewID = lastSb.Height + 1,
                prevViewID = lastSb.Height,
                requestSignature = Signatures.GetSignature(_sys.PosWallet.PrivateKey,
                    $"{lastSb.Hash}|{lastCons.Hash}", _sys.PosWallet.AccountId),
            };

            _context.Send2P2pNetwork(req);
            await CheckRequestAsync(view, req);
        }

        private void LookforVoters(View view)
        {
            // setup the voters list
            _context.RefreshAllNodesVotes();
            _context.Board.AllVoters = _context.Board.ActiveNodes
                .OrderByDescending(a => a.Votes)
                .Take(view.QualifiedNodeCount)
                .Select(a => a.AccountID)
                .ToList();
            _context.Board.AllVoters.Sort();
        }
    }


}
