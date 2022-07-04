using System.Linq;

namespace Lyra.Data.API.ODR
{
    public enum ResolutionType { OTCTrade };
    public class ODRResolution
    {
        public int Id { get; set; }

        /// <summary>
        /// account ID of the resolution owner
        /// </summary>
        public string Creator { get; set; } = null!;

        public ResolutionType RType { get; set; }
        public string TradeId { get; set; } = null!;
        // target
        public int CaseId { get; set; }

        public TransMove[] Actions { get; set; } = null!;

        /// <summary>
        /// say something about the resolution, optional the dispute itself
        /// </summary>
        public string? Description { get; set; }

        public string GetExtraData()
        {
            var actstr = string.Join("|", Actions.Select(x => x.GetExtraData()));
            return $"{Creator}|{RType}|{TradeId}|{CaseId}|{actstr}|{Description}";
        }

        public override string ToString()
        {
            var result = $"Creator: {Creator}\n";

            result += $"Resolution Type: {RType}\n";            
            result += $"On Trade: {TradeId}\n";
            result += $"On Dispute Case ID: {CaseId}\n";
            foreach(var act in Actions)
            {
                result += $"Action: {act}\n";
            }
            result += $"Description: {Description}";
            return result;
        }
    }
}
