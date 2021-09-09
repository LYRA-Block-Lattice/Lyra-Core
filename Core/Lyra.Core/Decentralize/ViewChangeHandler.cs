﻿using Lyra.Core.API;
using Lyra.Data.Crypto;
using Lyra.Core.Utils;
using Lyra.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Loyc.Collections;
using Lyra.Data;

namespace Lyra.Core.Decentralize
{
    public enum ViewChangeReason
    {
        // no heartbeat from leader
        LeaderNoHeartBeat,

        // no consolidate block created in time
        LeaderFailedConsolidating,

        // DEX request not processed in time
        LeaderFailedProcessingDEX,

        // user block no made consensus in time / liveness
        ConsensusTimeout,

        // view change not commited in time, not enough vote
        ViewChangeTimeout,

        // no service block created by the elected new leader in time
        NewLeaderFailedCreatingView,

        // f+1 view change request from the network
        TooManyViewChangeRquests,

        // we have new player join / leave
        PlayerJoinAndLeft,
    };

    public delegate void LeaderCandidateSelected(string candidate);
    public delegate void LeaderSelectedHandler(ViewChangeHandler sender, long _ViewId, string NewLeader, int Votes, List<string> Voters);

    public class ViewChangeHandler : ConsensusHandlerBase
    {
        private readonly LeaderCandidateSelected _candidateSelected;
        private readonly LeaderSelectedHandler _leaderSelected;

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

        public string nextLeader { get; private set; }

        public bool replySent = false;
        public bool commitSent = false;

        private ConcurrentDictionary<string, VCReqWithTime> reqMsgs { get; set; }
        private ConcurrentDictionary<string, VCReplyWithTime> replyMsgs { get; set; }
        private ConcurrentDictionary<string, VCCommitWithTime> commitMsgs { get; set; }
        public long ViewId { get; set; }

        private bool _isViewChanging = false;
        public bool IsViewChanging => _isViewChanging;
        private ViewChangeReason _reason;
        public ViewChangeReason LastViewChangeReason => _reason;

        DagSystem _sys;
        public ViewChangeHandler(DagSystem sys, ConsensusService context, LeaderCandidateSelected candidateSelected, LeaderSelectedHandler leaderSelected) : base(context)
        {
            _sys = sys;

            _candidateSelected = candidateSelected;
            _leaderSelected = leaderSelected;

            reqMsgs = new ConcurrentDictionary<string, VCReqWithTime>();
            replyMsgs = new ConcurrentDictionary<string, VCReplyWithTime>();
            commitMsgs = new ConcurrentDictionary<string, VCCommitWithTime>();
        }

        // debug only. should remove after
        public override bool IsTimeout => DateTime.Now - TimeStarted > TimeSpan.FromSeconds(LyraGlobal.VIEWCHANGE_TIMEOUT);

        public void Reset()
        {
            selectedSuccess = false;
            replySent = false;
            commitSent = false;
            ResetTimer();
            var r1 = reqMsgs.Where(a => a.Value.msg.ViewID != ViewId).Select(x => x.Key);
            r1.ForEach(x => reqMsgs.TryRemove(x, out _));

            var r2 = replyMsgs.Where(a => a.Value.msg.ViewID != ViewId).Select(x => x.Key);
            r2.ForEach(x => replyMsgs.TryRemove(x, out _));

            var r3 = commitMsgs.Where(a => a.Value.msg.ViewID != ViewId).Select(x => x.Key);
            r3.ForEach(x => commitMsgs.TryRemove(x, out _));
        }

        private void RemoveOutDatedMsgs()
        {
            var r1 = reqMsgs.Where(a => a.Value.msg.ViewID != ViewId || !_context.Board.AllVoters.Contains(a.Value.msg.From) || a.Value.msg.TimeStamp < DateTime.UtcNow.AddSeconds(-1 * LyraGlobal.VIEWCHANGE_TIMEOUT)).Select(x => x.Key);
            r1.ForEach(x => reqMsgs.TryRemove(x, out _));
            

            var r2 = replyMsgs.Where(a => a.Value.msg.ViewID != ViewId || !_context.Board.AllVoters.Contains(a.Value.msg.From) || a.Value.msg.TimeStamp < DateTime.UtcNow.AddSeconds(-1 * LyraGlobal.VIEWCHANGE_TIMEOUT)).Select(x => x.Key);
            r2.ForEach(x => replyMsgs.TryRemove(x, out _));

            var r3 = commitMsgs.Where(a => a.Value.msg.ViewID != ViewId || !_context.Board.AllVoters.Contains(a.Value.msg.From) || a.Value.msg.TimeStamp < DateTime.UtcNow.AddSeconds(-1 * LyraGlobal.VIEWCHANGE_TIMEOUT)).Select(x => x.Key);
            r3.ForEach(x => commitMsgs.TryRemove(x, out _));
        }

        protected override bool IsStateCreated()
        {
            return true;
        }

        internal async Task ProcessMessageAsync(ViewChangeMessage vcm)
        {
            if (ViewId != 0 && vcm.ViewID != ViewId)
            {
                _log.LogInformation($"VC Msgs: view ID {vcm.ViewID} not valid with {ViewId}");
                return;
            }

            if (vcm.TimeStamp < DateTime.UtcNow.AddSeconds(-1 * LyraGlobal.VIEWCHANGE_TIMEOUT))
                return;

            RemoveOutDatedMsgs();

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
            if(!IsViewChanging)
            {
                RemoveOutDatedMsgs();

                if (reqMsgs.Count > _context.Board.AllVoters.Count - LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
                {
                    var sb = new StringBuilder();
                    foreach (var msg in reqMsgs)
                        sb.Append($"{msg.Key.Shorten()}, ");

                    _log.LogInformation($"too many view change request, {sb.ToString()}. force into view change mode");

                    // too many view change request. force into view change mode
                    await _context.GotViewChangeRequestAsync(ViewId, reqMsgs.Count);
                }

                return;
            }

            // request
            if (!replySent && reqMsgs.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
            {
                _log.LogInformation($"CheckAllStats VID: {ViewId} Time: {TimeStarted} Req: {reqMsgs.Count} Reply: {replyMsgs.Count} Commit: {commitMsgs.Count} Votes {commitMsgs.Count}/{LyraGlobal.GetMajority(_context.Board.AllVoters.Count)}/{_context.Board.AllVoters.Count} Replyed: {replySent} Commited: {commitSent}");

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

            if (!commitSent)
            {
                if(replyMsgs.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
                {
                    _log.LogInformation($"CheckAllStats VID: {ViewId} Time: {TimeStarted} Req: {reqMsgs.Count} Reply: {replyMsgs.Count} Commit: {commitMsgs.Count} Votes {commitMsgs.Count}/{LyraGlobal.GetMajority(_context.Board.AllVoters.Count)}/{_context.Board.AllVoters.Count} Replyed: {replySent} Commited: {commitSent}");

                    // reply
                    // only if we have enough reply
                    var decQr = from rep in replyMsgs.Values
                             where rep.msg.Result == Blocks.APIResultCodes.Success
                             group rep by rep.msg.Candidate into g
                             select new { Candidate = g.Key, Count = g.Count() };

                    var decisions = decQr.OrderByDescending(x => x.Count).ToList();

                    var candidateQR = decisions.FirstOrDefault();

                    var sb = new StringBuilder();
                    sb.AppendLine($"Decisions for View ID: {ViewId}");
                    foreach (var x in decisions)
                    {
                        sb.AppendLine($"\t{x.Candidate.Shorten()}: {x.Count}");
                    }
                    _log.LogInformation(sb.ToString());

                    if (candidateQR?.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
                    {
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
                        //_log.LogInformation($"CheckAllStats, By ReplyMsgs, not commit, top candidate {candidateQR?.Candidate.Shorten()} has {candidateQR?.Count} votes");
                    }
                }
            }

            if(!selectedSuccess)
            {
                // commit
                var q = from rep in commitMsgs.Values
                        group rep by rep.msg.Candidate into g
                        select new { Candidate = g.Key, Count = g.Count() };

                var candidate = q.OrderByDescending(x => x.Count).FirstOrDefault();
                if(candidate != null)
                {
                    if (nextLeader != candidate.Candidate)
                    {
                        _log.LogWarning($"Next Leader {nextLeader} not {candidate.Candidate}");
                    }
                    nextLeader = candidate.Candidate;
                    if (candidate?.Count >= LyraGlobal.GetMajority(_context.Board.AllVoters.Count))
                    {
                        _log.LogInformation($"CheckAllStats, By CommitMsgs, leader selected {candidate.Candidate} with {candidate.Count} votes.");

                        selectedSuccess = true;
                        _leaderSelected(this, ViewId, candidate.Candidate, candidate.Count, _context.Board.AllVoters);
                    }
                }
            }
        }

        private async Task CheckCommitAsync(ViewChangeCommitMessage vcm)
        {
            if(!commitMsgs.ContainsKey(vcm.From))
            {
                var cmt = new VCCommitWithTime(vcm);
                commitMsgs.AddOrUpdate(vcm.From, cmt, (key, oldValue) => cmt);

                await CheckAllStatsAsync();
            }
        }

        private async Task CheckReplyAsync(ViewChangeReplyMessage reply)
        {
            //_log.LogInformation($"CheckReply for view {reply.ViewID} with Candidate {reply.Candidate.Shorten()} of {replyMsgs.Count}/{_context.Board.AllVoters.Count}");

            if (reply.Result == Blocks.APIResultCodes.Success)
            {
                if (replyMsgs.ContainsKey(reply.From))
                {
                    //if (replyMsgs[reply.From].msg.Candidate != reply.Candidate)
                    //{
                    //    replyMsgs[reply.From] = new VCReplyWithTime(reply);
                    //    await CheckAllStatsAsync();
                    //}
                }
                else
                {
                    if(replyMsgs.TryAdd(reply.From, new VCReplyWithTime(reply)))
                        await CheckAllStatsAsync();
                }
            }
        }

        private async Task CheckRequestAsync(ViewChangeRequestMessage req)
        {
            //_log.LogInformation($"CheckRequestAsync from {req.From.Shorten()} for view {req.ViewID} Signature {req.requestSignature.Shorten()}");

            // make sure all request from all voters
            if (!_context.Board.AllVoters.Contains(req.From))
                return;

            if (reqMsgs.Values.Any(a => a.msg.From == req.From))
            {
                reqMsgs.TryRemove(req.From, out _);
            }

            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();
            var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();

            if (Signatures.VerifyAccountSignature($"{lastSb.Hash}|{lastCons.Hash}", req.From, req.requestSignature))
            {
                if(reqMsgs.TryAdd(req.From, new VCReqWithTime(req)))
                    await CheckAllStatsAsync();
            }
            //else
            //    _log.LogWarning($"ViewChangeRequest signature verification failed from {req.From.Shorten()}");
        }

        /// <summary>
        /// two ways to begin view changing: either one third of all voters requested, or local requested.
        /// 
        /// </summary>
        /// <returns></returns>
        internal async Task BeginChangeViewAsync(ViewChangeReason reason)
        {
            _log.LogInformation($"BeginChangeViewAsync: VID: {ViewId} Req: {reqMsgs.Count} Reply: {replyMsgs.Count} Commit: {commitMsgs.Count} Votes {commitMsgs.Count}/{LyraGlobal.GetMajority(_context.Board.AllVoters.Count)}/{_context.Board.AllVoters.Count} Replyed: {replySent} Commited: {commitSent}");

            _reason = reason;
            var lastSb = await _sys.Storage.GetLastServiceBlockAsync();

            if (lastSb == null)
            {
                // genesis?
                _log.LogCritical($"BeginChangeViewAsync has null service block. should not happend. error.");
                return;
            }

            _isViewChanging = true;
            
            ShiftView(lastSb.Height + 1);
            selectedSuccess = false;

            _log.LogInformation($"View change for ViewId {ViewId} begin at {TimeStarted}");

            CalculateLeaderCandidate();

            var lastCons = await _sys.Storage.GetLastConsolidationBlockAsync();
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

        public void StopViewChange()
        {
            _isViewChanging = false;
            Reset();
            ResetTimer();
        }

        private void CalculateLeaderCandidate()
        {
            // the new leader:
            // 1, not the previous one;
            // 2, viewid mod [voters count], index of _qualifiedVoters.
            // 
            // refresh billboard all voters
            _context.UpdateVoters();

            int leaderIndex = (int)(ViewId % _context.Board.AllVoters.Count);
            nextLeader = _context.Board.AllVoters[leaderIndex];

            // we have already excluded the failed leader. x don't exclude. replace index with hash algo
            if (_context.IsLeaderInFailureList(nextLeader))
            {
                leaderIndex = Utilities.Sha256Int($"{ViewId}") % _context.Board.AllVoters.Count;
                nextLeader = _context.Board.AllVoters[leaderIndex];
            }

            _log.LogInformation($"The Next leader will be {nextLeader}");
            _candidateSelected(nextLeader);
        }

        internal void ShiftView(long v)
        {
            _log.LogInformation($"ShiftView to {v}");
            Reset();
            ViewId = v;
            ResetTimer();
        }

        public void FinishViewChange(long v)
        {
            if(ViewId == v)
                _isViewChanging = false;
        }
    }
}
