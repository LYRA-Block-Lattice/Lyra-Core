using Neo.Wallets;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Lyra.Core.Cryptography
{
    //
    // Parts of this code are from https://github.com/sander-/working-with-digital-signatures
    //
    public class Signatures
    {
        private static bool IsMono { get; }
        static Signatures()
        {
            IsMono = true;// Type.GetType("Mono.Runtime") != null;
        }
        public static bool ValidateAccountId(string AccountId)
        {
            try
            {
                if (AccountId[0] != 'L')
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
            var signatureBytes = Base58Encoding.Decode(signature);
            var publicKeyBytes = Base58Encoding.DecodeAccountId(AccountId);

            return Neo.Cryptography.Crypto.Default.VerifySignature(Encoding.UTF8.GetBytes(message),
                signatureBytes, publicKeyBytes);
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
            //var sigInt = sa.GenerateSignature(Encoding.UTF8.GetBytes(message));
            //sigInt.            
        }

        public static (string privateKey, string AccountId) GenerateWallet()
        {
            byte[] privateKey = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(privateKey);
            }
            var kp = new KeyPair(privateKey);

            var pvtKeyStr = Base58Encoding.EncodePrivateKey(privateKey);

            var pubKey = kp.PublicKey.EncodePoint(false).Skip(1).ToArray();
            return (pvtKeyStr, Base58Encoding.EncodeAccountId(pubKey));
        }

        public static string GetAccountIdFromPrivateKey(string privateKey)
        {
            var pvtKey = Base58Encoding.DecodePrivateKey(privateKey);
            var kp = new Neo.Wallets.KeyPair(pvtKey);
            return Base58Encoding.EncodeAccountId(kp.PublicKey.EncodePoint(false).Skip(1).ToArray());
        }
    }
}
