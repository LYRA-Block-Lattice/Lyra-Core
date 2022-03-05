using Lyra.Data.API.WorkFlow;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ODR
{
    public class VotingSummary
    {
        public VotingGenesisBlock Spec { get; set; }
        public List<VotingBlock> Votes { get; set; }

        public bool IsDecided
        {
            get
            {
                if (Votes.Count == 0)
                    return false;

                var r = Votes.GroupBy(a => a.OptionIndex)
                    .Select(g => new
                    {
                        Index = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(a => a.Count);

                return r.First().Count >= Math.Round((decimal)Votes.Count / 3 * 2, 0, MidpointRounding.ToPositiveInfinity);
            }
        }

        public int DecidedIndex
        {
            get
            {
                if (!IsDecided)
                    throw new Exception("Undeterminated Vote");

                var r = Votes.GroupBy(a => a.OptionIndex)
                    .Select(g => new
                    {
                        Index = g.Key,
                        Count = g.Count()
                    })
                    .OrderByDescending(a => a.Count);

                return r.First().Index;
            }
        }
    }
}
