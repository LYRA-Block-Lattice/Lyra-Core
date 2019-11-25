using Lyra.Core.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra.Node2.Accounts
{
    // transaction by exchange account is zero-fee
    public class ExchangeAccountWallet : Wallet
    {
        public ExchangeAccountWallet(IAccountDatabase storage, string NetworkId) : base(storage, NetworkId)
        {
        }
    }
}
