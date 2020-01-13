using Lyra.Core.Cryptography;
using System;
using System.Collections.Generic;
using System.Text;

namespace Friday
{
    /// <summary>
    /// the master wallet is "My Account", which has 100M Lyra.Coin and 1000B Friday.Coin
    /// so we create 1000 wallet named [Friday1-Friday1000], transfer 1000 Friday.Coin and 10 Lyra.Coin to it.
    /// </summary>
    public class TransactionTester
    {
        public Dictionary<string, string> CreateWallet(int count)
        {
            var dict = new Dictionary<string, string>();
            for(int i = 1; i <= count; i++)
            {
                var nw = Signatures.GenerateWallet();
                dict.Add(nw.AccountId, nw.privateKey);                
            }
            return dict;
        }
    }
}
