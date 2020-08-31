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
    public delegate void LeaderSelectedHandler(ViewChangeHandler sender, long _ViewId, string NewLeader, int Votes, List<string> Voters);

    public class ViewChangeHandler : ConsensusHandlerBase
    {
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

        public bool selectedSuccess = false;

        public string nextLeader;

        public bool replySent = false;
        public bool commitSent = false;

        private ConcurrentDictionary<string, VCReqWithTime> reqMsgs { get; set; }
        private ConcurrentDictionary<string, VCReplyWithTime> replyMsgs { get; set; }
        private ConcurrentDictionary<string, VCCommitWithTime> commitMsgs { get; set; }
        public long ViewId { get; set; }

        DagSystem _sys;
        public ViewChangeHandler(DagSystem sys, ConsensusService context, LeaderSelectedHandler leaderSelected) : base(context)
        {
            _sys = sys;

            _leaderSelected = leaderSelected;

            TimeStarted = DateTime.MinValue;

            reqMsgs = new ConcurrentDictionary<string, VCReqWithTime>();
            replyMsgs = new ConcurrentDictionary<string, VCReplyWithTime>();
            commitMsgs = new ConcurrentDictionary<string, VCCommitWithTime>();
        }

        // debug only. should remove after
        public override bool CheckTimeout()
        {
            if (TimeStarted != DateTime.MinValue && DateTime.Now - TimeStarted > TimeSpan.FromSeconds(LyraGlobal.VIEWCHANGE_TIMEOUT))
            {
                return true;
            }
            else
                return false;
        }

        public void Reset()
        {
            replySent = false;
            commitSent = false;
            TimeStarted = DateTime.MinValue;
            reqMsgs.Clear();
            replyMsgs.Clear();
            commitMsgs.Clear();
        }

        protected override bool IsStateCreated()
        {
            return true;
        }

        internal async Task ProcessMessage(ViewChangeMessage vcm)
        {
            if (selectedSuccess)
                return;

            _log.LogInformation($"ProcessMessage type: {vcm.MsgType} from: {vcm.From.Shorten()}");
            if (ViewId != 0 && vcm.ViewID != ViewId)
            {
                _log.LogInformation($"ProcessMessage: view ID {vcm.ViewID} not valid with {ViewId}");
                return;
            }

            if (vcm is ViewChangeRequestMessage req)
            {
                await CheckRequestAsync(req);
                return;
            }

            //_log.LogInformation($"ViewChangeHandler ProcessMessage From {vcm.From.Shorten()} with ViewID {vcm.ViewID} My ViewID {_viewId} ");

            if (_context.Board.AllVoters.Contains(vcm.From))      // not the next one
            {
                switch (vcm)
                {
                    case ViewChangeReplyMessage reply:
                        await CheckReplyAsync(reply);
                        break;
                    case ViewChangeCommitMessage commit:
                        await CheckCommitAsync(commit);
                        break;
                    default:
                        _log.LogWarning("Should not happen.");
                        break;
                }
            }
        }

        private async Task CheckAllStatsAsync()
        {
            _log.LogInformation($"CheckAllStats VID: {ViewId} Req: {reqMsgs.Count} Reply: {replyMsgs.Count} Commit: {commitMsgs.Count} Votes {commitMsgs.Count}/{LyraGlobal.GetMajority(_context.Board.AllVoters.Count)}/{_context.Board.AllVoters.Count} Replyed: {replySent} Commited: {commitSent}");

            if (selectedSuccess)
                return;

            // remove outdated msgs
            var q1 = reqMsgs.Where(a => a.Value.Time < DateTime.Now.AddSeconds(-15))
                .Select(b => b.Key)
                .ToList();
            foreach (var req in q1)
                reqMsgs.TryRemove(req, out _);

            var q2 = replyMsgs.Where(a => a.Value.Time < DateTime.Now.AddSeconds(-15))
                .Select(b => b.Key)
                .ToList();
            foreach (var req in q2)
                replyMsgs.TryRemove(req, out _);

            var q3 = commitMsgs.Where(a => a.Value.Time < DateTime.Now.AddSeconds(-15))
                .Select(b => b.Key)
                .ToList();
            foreach (var req in q3)
                commitMsgs.TryRemove(req, out _);

            _log.LogInformation($"CheckAllStats VID: {ViewId} Req: {reqMsgs.Count} Reply: {replyMsgs.Count} Commit: {commitMsgs.Count} Votes {commitMsgs.Count}/{LyraGlobal.GetMajority(_context.Board.AllVoters.Count)}/{_context.Board.AllVoters.Count} Replyed: {replySent} Commited: {commitSent}");

            // request
            if (!replySent && reqMsgs.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                // the new leader:
                // 1, not the previous one;
                // 2, viewid mod [voters count], index of _qualifiedVoters.
                // 
                var leaderIndex = (int)(ViewId % _context.Board.AllVoters.Count);

                do
                {
                    var leader = _context.Board.AllVoters[leaderIndex];
                    if (!reqMsgs.Values.Any(a => a.msg.From == leader))     // it is offline
                    {
                        leaderIndex = (leaderIndex + 1) % _context.Board.AllVoters.Count;
                    }
                    else
                        break;
                } while (true);

                nextLeader = _context.Board.AllVoters[leaderIndex];
                _log.LogInformation($"CheckAllStats, By ReqMsgs, next leader will be {nextLeader}");

                var reply = new ViewChangeReplyMessage
                {
                    From = _sys.PosWallet.AccountId,
                    ViewID = ViewId,
                    Result = Blocks.APIResultCodes.Success,
                    Candidate = nextLeader
                };

                _context.Send2P2pNetwork(reply);

                replySent = true;
                await CheckReplyAsync(reply);
            }
            else if (reqMsgs.Count > _context.Board.AllVoters.Count - LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                if (TimeStarted == DateTime.MinValue)
                {
                    _log.LogInformation("too many view change request. force into view change mode");
                    TimeStarted = DateTime.Now;
                    // too many view change request. force into view change mode
                    await _context.GotViewChangeRequestAsync(ViewId);

                    // also do clean of req msgs queue
                    var unqualifiedReqs = reqMsgs.Keys.Where(a => !_context.Board.AllVoters.Contains(a));
                    foreach (var unq in unqualifiedReqs)
                        reqMsgs.Remove(unq, out _);
                }
            }

            if (!commitSent)
            {
                // reply
                // only if we have enough reply
                var qr = from rep in replyMsgs.Values
                         where rep.msg.Result == Blocks.APIResultCodes.Success
                         group rep by rep.msg.Candidate into g
                         select new { Candidate = g.Key, Count = g.Count() };

                var candidateQR = qr.OrderByDescending(x => x.Count).FirstOrDefault();

                if (candidateQR?.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
                {
                    _log.LogInformation($"CheckAllStats, By ReplyMsgs, commit next leader {nextLeader}");

                    var commit = new ViewChangeCommitMessage
                    {
                        From = _sys.PosWallet.AccountId,
                        ViewID = ViewId,
                        Candidate = candidateQR.Candidate,
                        Consensus = ConsensusResult.Yea
                    };

                    _context.Send2P2pNetwork(commit);
                    commitSent = true;
                    await CheckCommitAsync(commit);
                }
                else
                {
                    _log.LogInformation($"CheckAllStats, By ReplyMsgs, not commit: top candidate {candidateQR?.Candidate.Shorten()} has {candidateQR?.Count} votes");
                }
            }

            // commit
            var q = from rep in commitMsgs.Values
                    group rep by rep.msg.Candidate into g
                    select new { Candidate = g.Key, Count = g.Count() };

            var candidate = q.FirstOrDefault();
            if (candidate?.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                _log.LogInformation($"CheckAllStats, By CommitMsgs, leader selected {candidate.Candidate} with {candidate.Count} votes.");

                selectedSuccess = true;
                _leaderSelected(this, ViewId, candidate.Candidate, candidate.Count, _context.Board.AllVoters);
            }
        }

        private async Task CheckCommitAsync(ViewChangeCommitMessage vcm)
        {
            if (!commitMsgs.ContainsKey(vcm.From))
            {
                var cmt = new VCCommitWithTime(vcm);
                commitMsgs.AddOrUpdate(vcm.From, cmt, (key, oldValue) => cmt);

                _log.LogInformation($"CheckCommit from {vcm.From.Shorten()} for view {vcm.ViewID} with Candidate {vcm.Candidate.Shorten()} of {commitMsgs.Count}/{LyraGlobal.GetMajority(_context.Board.AllVoters.Count)}/{_context.Board.AllVoters.Count}");

                await CheckAllStatsAsync();
            }
        }

        private async Task CheckReplyAsync(ViewChangeReplyMessage reply)
        {
            //_log.LogInformation($"CheckReply for view {reply.ViewID} with Candidate {reply.Candidate.Shorten()} of {_replyMsgs.Count}/{_qualifiedVoters.Count}");

            if (reply.Result == Blocks.APIResultCodes.Success)
            {
                if (replyMsgs.ContainsKey(reply.From))
                {
                    if (replyMsgs[reply.From].msg.Candidate != reply.Candidate)
                    {
                        replyMsgs[reply.From] = new VCReplyWithTime(reply);
                        await CheckAllStatsAsync();
                    }
                }
                else
                {
                    replyMsgs.TryAdd(reply.From, new VCReplyWithTime(reply));
                    await CheckAllStatsAsync();
                }
            }
        }

        private async Task CheckRequestAsync(ViewChangeRequestMessage req)
        {
            //_log.LogInformation($"CheckRequestAsync from {req.From.Shorten()} for view {req.ViewID} Signature {req.requestSignature.Shorten()}");

            if (!reqMsgs.Values.Any(a => a.msg.From == req.From))
            {
                var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
                var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();

                if (Signatures.VerifyAccountSignature($"{lastSb.Hash}|{lastCons.Hash}", req.From, req.requestSignature))
                {
                    reqMsgs.TryAdd(req.From, new VCReqWithTime(req));
                    await CheckAllStatsAsync();
                }
                else
                    _log.LogWarning($"ViewChangeRequest signature verification failed from {req.From.Shorten()}");
            }
        }

        /// <summary>
        /// two ways to begin view changing: either one third of all voters requested, or local requested.
        /// 
        /// </summary>
        /// <returns></returns>
        internal async Task BeginChangeViewAsync()
        {
            _log.LogInformation($"Begin Change ");
            _log.LogInformation($"AllStats VID: {ViewId} Req: {reqMsgs.Count} Reply: {replyMsgs.Count} Commit: {commitMsgs.Count} Votes {commitMsgs.Count}/{LyraGlobal.GetMajority(_context.Board.AllVoters.Count)}/{_context.Board.AllVoters.Count} Replyed: {replySent} Commited: {commitSent}");

            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();

            if (lastSb == null)
            {
                // genesis?
                _log.LogCritical($"BeginChangeViewAsync has null service block. should not happend. error.");
                return;
            }

            // refresh billboard all voters
            _context.UpdateVoters();

            var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();

            ViewId = lastSb.Height + 1;
            TimeStarted = DateTime.Now;
            selectedSuccess = false;
            
            _log.LogInformation($"View change begin at {TimeStarted}");

            var req = new ViewChangeRequestMessage
            {
                From = _sys.PosWallet.AccountId,
                ViewID = ViewId,
                prevViewID = lastSb.Height,
                requestSignature = Signatures.GetSignature(_sys.PosWallet.PrivateKey,
                    $"{lastSb.Hash}|{lastCons.Hash}", _sys.PosWallet.AccountId),
            };

            _context.Send2P2pNetwork(req);
            await CheckRequestAsync(req);
        }

        internal void ShiftView(long v)
        {
            ViewId = v;
            TimeStarted = DateTime.MinValue;
        }
    }


}
