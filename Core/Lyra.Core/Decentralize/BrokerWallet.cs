using Lyra.Core.Accounts;
using Lyra.Data.API;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Decentralize
{
    // make a broker account looks like a wallet
    public class BrokerWallet : Wallet
    {
        protected BrokerWallet(IAccountDatabase storage, string name, ILyraAPI rpcClient = null)
            : base(storage, name, rpcClient)
        {

        }
    }
}
