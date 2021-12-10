using Lyra.Core.Accounts;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public class AccountInBuffer : AccountInMemoryStorage
    {
        public AccountInBuffer()
        {

        }
        public AccountInBuffer(byte[] buff, string password)
        {
            var s = Encoding.ASCII.GetString(buff);

            var enc = new AesBcCrypto();
            var output = enc.Decrypt(s, password);
            var secs = output.Split('|', 5);

            _privateKey = secs[0];
            _networkId = secs[1];
            _accountId = secs[2];
            _voteFor = secs[3];
            _accountName = secs[4];
        }
        public byte[] GetBuffer(string password)
        {
            var s = $"{PrivateKey}|{NetworkId}|{AccountId}|{VoteFor}|{Name}";

            var enc = new AesBcCrypto();
            var output = enc.Encrypt(s, password);

            return Encoding.ASCII.GetBytes(output);
        }
    }
}
