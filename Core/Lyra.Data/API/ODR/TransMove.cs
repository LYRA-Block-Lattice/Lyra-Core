using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ODR
{
    public enum FundSources { Buyer, Seller, DAOTreasure }
    public class TransMove
    {
        public FundSources source { get; set; }
        public string to { get; set; } = null!;
        public decimal amount { get; set; }
        public string? desc { get; set; }

        public string GetExtraData()
        {
            return $"{source}|{to}|{amount.ToBalanceLong()}|{desc}";
        }

        public override string ToString()
        {
            var result = $"from: {source}\n";
            result += $"to: {to}\n";
            result += $"amount: {amount}\n";
            result += $"desc: {desc}\n";
            return result;
        }
    }
}
