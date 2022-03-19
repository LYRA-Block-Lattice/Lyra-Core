using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ODR
{
    public enum Parties { Buyer, Seller, DAOTreasure }
    public class TransMove
    {
        public Parties from { get; set; }
        public Parties to { get; set; }
        public decimal amount { get; set; }
        public string? desc { get; set; }

        public string GetExtraData()
        {
            return $"{from}|{to}|{amount.ToBalanceLong()}|{desc}";
        }

        public override string ToString()
        {
            var result = $"from: {from}\n";
            result += $"to: {to}\n";
            result += $"amount: {amount}\n";
            result += $"desc: {desc}\n";
            return result;
        }
    }
}
