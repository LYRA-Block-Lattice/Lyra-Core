using Lyra.Core.Accounts;
using Lyra.Core.Cryptography;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lyra.Authorizer.Accounts
{
    // transaction by exchange account is zero-fee
    public class ExchangeAccountWallet : Wallet
    {
        public ExchangeAccountWallet(ISignatures signr, IAccountDatabase storage, string NetworkId) : base(signr, storage, NetworkId)
        {
        }
    }
}
