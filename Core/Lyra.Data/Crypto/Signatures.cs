using Lyra.Core.API;
using Neo.Wallets;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Lyra.Data.Crypto
{
    public class Signatures
    {
        private static ILyraSignature _signr { get; set; }

        static Signatures()
        {
            Switch(false);
        }

        public static void Switch(bool usebc)
        {
            if (usebc)
                _signr = new BCSignature();
            else
                _signr = new DotnetSignature();
        }

        public static bool ValidateAccountId(string AccountId) => _signr.ValidateAccountId(AccountId);
        public static bool ValidatePublicKey(string PublicKey) => _signr.ValidatePublicKey(PublicKey);
        public static bool ValidatePrivateKey(string PrivateKey) => _signr.ValidatePrivateKey(PrivateKey);

        public static bool VerifyAccountSignature(string message, string accountId, string signature)
            => _signr.VerifyAccountSignature(message, accountId, signature);

        public static string GetSignature(string privateKey, string message, string AccountId)
            => _signr.GetSignature(privateKey, message, AccountId);
        public static (string privateKey, string AccountId) GenerateWallet(byte [] keyData)
            => _signr.GenerateWallet(keyData);

        public static (string privateKey, string AccountId) GenerateWallet() => _signr.GenerateWallet();

        public static string GetAccountIdFromPrivateKey(string privateKey) => _signr.GetAccountIdFromPrivateKey(privateKey);
    }
}
