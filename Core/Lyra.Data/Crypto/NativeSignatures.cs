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
    //
    // Parts of this code are from https://github.com/sander-/working-with-digital-signatures
    //
    public class NativeSignatures
    {
        //private static ILogger _log;
        private static bool IsMono { get; }
        static NativeSignatures()
        {
            try
            {
                IsMono = Type.GetType("Mono.Runtime") != null;
                //_log = new SimpleLogger("Signatures").Logger;
            }
            catch(Exception)
            {

            }
        }
        public static bool ValidateAccountId(string AccountId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(AccountId) || AccountId[0] != LyraGlobal.ADDRESSPREFIX)
                    return false;

                Base58Encoding.DecodeAccountId(AccountId);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // It can validate either public or private key - thanks to the checksum
        public static bool ValidatePublicKey(string PublicKey)
        {
            try
            {
                Base58Encoding.DecodePublicKey(PublicKey);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ValidatePrivateKey(string PrivateKey)
        {
            try
            {
                Base58Encoding.DecodePrivateKey(PrivateKey);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool VerifyAccountSignature(string message, string accountId, string signature)
        {
            if (IsMono)
                return PortableSignatures.VerifyAccountSignature(message, accountId, signature);

            if (string.IsNullOrWhiteSpace(message) || !ValidateAccountId(accountId) || string.IsNullOrWhiteSpace(signature))
                return false;

            return VerifySignature(message, accountId, signature);
        }

        private static bool VerifySignature(string message, string AccountId, string signature)
        {
            try
            {
                var signatureBytes = Base58Encoding.Decode(signature);
                var publicKeyBytes = Base58Encoding.DecodeAccountId(AccountId);

                var result = Neo.Cryptography.Crypto.Default.VerifySignature(Encoding.UTF8.GetBytes(message), signatureBytes, publicKeyBytes);

                return result;
            }
            catch
            {
                //_log?.LogError("VerifySignature failed: " + ex.Message);
                return false;
            }
        }

        public static string GetSignature(string privateKey, string message, string AccountId)
        {
            if (IsMono)
                return PortableSignatures.GetSignature(privateKey, message);

            var publicKeyBytes = Base58Encoding.DecodeAccountId(AccountId);
            var privateKeyBytes = Base58Encoding.DecodePrivateKey(privateKey);
            var signature = Neo.Cryptography.Crypto.Default.Sign(Encoding.UTF8.GetBytes(message), privateKeyBytes, publicKeyBytes);
            return Base58Encoding.Encode(signature);

            //Neo.Cryptography.ECC.ECDsa sa = new Neo.Cryptography.ECC.ECDsa(privateKeyBytes, Neo.Cryptography.ECC.ECCurve.Secp256r1);
            //var sigInts = sa.GenerateSignature(Encoding.ASCII.GetBytes(message));

            //var sh = new SignatureHolder(sigInts);
            //var signature = sh.ToString();

            ////var vrt = VerifySignature(message, AccountId, signature);

            //return signature;
        }

        private class SignatureHolder
        {
            public BigInteger R { get; set; }
            public BigInteger S { get; set; }

            public SignatureHolder(BigInteger[] bigs) : this(bigs[0], bigs[1])
            {

            }

            public SignatureHolder(BigInteger r, BigInteger s)
            {
                R = r;
                S = s;
            }

            public SignatureHolder(string signature)
            {
                var buff = Base58Encoding.Decode(signature);
                var b1 = new byte[buff[0]];
                var b2 = new byte[buff[1]];
                Buffer.BlockCopy(buff, 2, b1, 0, b1.Length);
                Buffer.BlockCopy(buff, 2 + b1.Length, b2, 0, b2.Length);

                R = new BigInteger(b1);
                S = new BigInteger(b2);
            }

            public override string ToString()
            {
                var b1 = R.ToByteArray();
                var b2 = S.ToByteArray();
                var buff = new byte[2 + b1.Length + b2.Length];
                buff[0] = (byte)b1.Length;
                buff[1] = (byte)b2.Length;
                Buffer.BlockCopy(b1, 0, buff, 2, b1.Length);
                Buffer.BlockCopy(b2, 0, buff, 2 + b1.Length, b2.Length);
                return Base58Encoding.Encode(buff);
            }
        }

        public static (string privateKey, string AccountId) GenerateWallet(byte [] keyData)
        {
            var kp = new KeyPair(keyData);

            var pvtKeyStr = Base58Encoding.EncodePrivateKey(keyData);

            var pubKey = kp.PublicKey.EncodePoint(false).Skip(1).ToArray();
            return (pvtKeyStr, Base58Encoding.EncodeAccountId(pubKey));
        }

        public static (string privateKey, string AccountId) GenerateWallet()
        {
            byte[] keyData = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(keyData);
            }
            return GenerateWallet(keyData);
        }

        public static string GetAccountIdFromPrivateKey(string privateKey)
        {
            var pvtKey = Base58Encoding.DecodePrivateKey(privateKey);
            var kp = new Neo.Wallets.KeyPair(pvtKey);
            return Base58Encoding.EncodeAccountId(kp.PublicKey.EncodePoint(false).Skip(1).ToArray());
        }
    }
}
