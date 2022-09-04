using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.Identity
{
    /// <summary>
    /// None -> Peer: any time
    /// Peer -> DAO: 24 hours
    /// DAO -> Lyra Council: 4 days
    /// 
    /// Peer Level: peer must response within 12 hours or will be fine 10% of lost.
    /// DAO Level: Dao owner must response within 2 days or will be fine 20% of lost.
    /// Council Level: The Council must response within 5 days or will be fine 100% of lost.
    /// </summary>
    public enum DisputeLevels { None, Peer, DAO, LyraCouncil }

    public enum DisputeNegotiationStates
    {
        NewlyCreated,
        AllPartiesNotified,
        AcceptanceConfirmed,
        Executed,   // after a yay acceptance
        Failed,    // after a nay acceptance
    }
    /// <summary>
    /// lost is calculated in LYR
    /// </summary>
    public abstract class DisputeCase
    {
        /// <summary>
        /// Case ID, start from 1.
        /// </summary>
        public int Id { get; set; }
        public DateTime RaisedTime { get; set; }

        public DisputeNegotiationStates State { get; set; }

        public (string complaintant, string respondant) GetRoles(IOtcTrade trade)
        {
            if (trade.OwnerAccountId == Complaint.ownerId)
                return (trade.OwnerAccountId, trade.Trade.orderOwnerId);
            else
                return (trade.Trade.orderOwnerId, trade.OwnerAccountId);
        }
        // plaintiff
        public ComplaintClaim Complaint { get; set; } = null!;

        public virtual bool Verify(IOtcTrade trade)
        {
            return trade.AccountID == Complaint.tradeId && 
                (trade.OwnerAccountId == Complaint.ownerId || trade.Trade.orderOwnerId == Complaint.ownerId) &&
                Complaint.VerifySignature(Complaint.ownerId);
        }

        public abstract bool GetAllowCancel();
            

        //public decimal ClaimedLost { get; set; }

        //// defendant
        //public bool PeerAcceptance { get; set; }
        //public DateTime PeerAcceptanceTime { get; set; }   
        
        //// mediator
        //public string? MediatorID { get; set; }
        //public ODRResolution? Resolution { get; set; }
        //public DateTime ResolutionTime { get; set; }

        //// final result
        //public bool AcceptanceByPlaintiff { get; set; }
        //public DateTime AcceptanceTimeByPlaintiff { get; set; }

        //public bool AcceptanceByDefendant { get; set; }
        //public DateTime AcceptanceTimeByDefendant { get; set; }
    }

    public class PeerDisputeCase : DisputeCase
    {
        public ComplaintReply? Reply { get; set; }

        public override bool GetAllowCancel()
        {
            return Reply != null && Complaint.request == ComplaintRequest.CancelTrade
                && Reply.response == ComplaintResponse.AgreeCancel;
        }

        public override bool Verify(IOtcTrade trade)
        {
            (var complaintantId, var respondantId) = GetRoles(trade);
            return base.Verify(trade) &&
                ((Reply?.tradeId ?? trade.AccountID) == trade.AccountID) &&
                ((Reply?.ownerId ?? respondantId) == respondantId) &&
                (Reply?.VerifySignature(Reply.ownerId) ?? true);
        }
    }

    public class DaoDisputeCase : DisputeCase
    {
        public string VoteId { get; set; }
        public ODRResolution? Resolution { get; set; }
        public List<ComplaintReply>? Replies { get; set; }
        public DateTime LastUpdateTime { get; set; }

        public override bool GetAllowCancel()
        {
            return false;
        }

        public override bool Verify(IOtcTrade trade)
        {
            (var complaintantId, var respondantId) = GetRoles(trade);

            if (!base.Verify(trade))
                return false;

            foreach(var reply in Replies ?? Enumerable.Empty<ComplaintReply>())
            {
                // reply from complaintant
                if (reply.ownerId == complaintantId)
                {
                    if (reply.tradeId != trade.AccountID || !reply.VerifySignature(reply.ownerId))
                        return false;
                }
                else if (reply.ownerId == respondantId)
                {
                    // reply from respondant
                    if (reply.tradeId != trade.AccountID || !reply.VerifySignature(reply.ownerId))
                        return false;
                }
                else
                    return false;
            }

            return true;
        }
    }

    public class CouncilDisputeCase : DaoDisputeCase
    {
        public bool AutoExecute { get; set; }
        public CouncilDisputeCase()
        {
            AutoExecute = false;
        }
    }
}
