using Lyra.Core.Blocks;
using Org.BouncyCastle.Asn1.X509.Qualified;
using System.Data;
using System;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Collections.Generic;

namespace Lyra.Data.API.ODR
{
    public enum ResolutionType { OTCTrade };
    public enum ResolutionStatus { Pending, Success, Failed };

    public class ResolutionContainer
    {
        public ODRResolution Resolution { get; set; } = null!;
        public ResolutionStatus Status { get; set; }
        public List<AnswerToResolution> Replies { get; set; } = null!;
    }

    /// <summary>
    /// resolution belongs to trading room. it will cover all current complaint.
    /// if resolution don't include all complaint, will will failed to submit.
    /// </summary>
    public class ODRResolution : SignableObject
    {
        public int Id { get; set; }

        /// <summary>
        /// account ID of the resolution owner
        /// </summary>
        public string Creator { get; set; } = null!;

        public ResolutionType RType { get; set; }
        public string TradeId { get; set; } = null!;
        // target
        public string [] ComplaintHashes { get; set; } = null!;

        public TransMove[] Actions { get; set; } = null!;

        /// <summary>
        /// say something about the resolution, optional the dispute itself
        /// </summary>
        public string? Description { get; set; }

        public override string GetHashInput()
        {
            return GetExtraData();
        }

        protected override string GetExtraData()
        {
            var actstr = string.Join("|", Actions.Select(x => x.GetExtraData()));
            return $"{Creator}|{RType}|{TradeId}|{string.Join(",", ComplaintHashes)}|{actstr}|{Convert.ToBase64String(Encoding.UTF8.GetBytes(Description??""))}";
        }

        public override string ToString()
        {
            var result = $"Creator: {Creator}\n";

            result += $"Resolution Type: {RType}\n";
            result += $"On Trade: {TradeId}\n";
            result += $"On Complaint Hashes: {string.Join(",", ComplaintHashes)}\n";
            foreach(var act in Actions)
            {
                result += $"Action: {act}\n";
            }
            result += $"Description: {Description}";
            return result;
        }
    }
}
