using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ABI
{
    public class FiatCreateWallet
    {
        public string symbol { get; set; } = null!;
    }

    public class FiatPrintMoney
    {
        public string symbol { get; set; } = null!;
        public decimal amount { get; set; }    
    }
}
