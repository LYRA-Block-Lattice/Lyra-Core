using System;
using System.Text;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Math;
using Lyra.Core.API;
using System.Linq;

namespace Lyra.Data.Crypto
{
    //
    // Parts of this code are from https://github.com/sander-/working-with-digital-signatures
    //
    public static class PortableSignatures
    {
        public static bool ValidateAccountId(string AccountId)
        {
            try
            {
                if (AccountId[0] != LyraGlobal.ADDRESSPREFIX)
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
            if (string.IsNullOrWhiteSpace(message) || !ValidateAccountId(accountId) || string.IsNullOrWhiteSpace(signature))
                return false;
            var publicKeyBytes = Base58Encoding.DecodeAccountId(accountId);
            return VerifySignature(message, publicKeyBytes, signature);
        }

        public static bool VerifyAuthorizerSignature(string message, string publicKey, string signature)
        {
            if (string.IsNullOrWhiteSpace(message) || !ValidatePublicKey(publicKey) || string.IsNullOrWhiteSpace(signature))
                return false;
            var publicKeyBytes = Base58Encoding.DecodePublicKey(publicKey);
            return VerifySignature(message, publicKeyBytes, signature);
        }

        private static bool VerifySignature(string message, byte[] public_key_bytes, string signature)
        {

            var curve = SecNamedCurves.GetByName("secp256r1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

            //var publicKeyBytes = Base58Encoding.Decode(publicKey);
            //var publicKeyBytes = Base58Encoding.DecodeWithCheckSum(publicKey);
            //var publicKeyBytes = Base58Encoding.DecodePublicKey(publicKey);

            var byte0 = new byte[public_key_bytes.Length+1];
            byte0[0] = 4;
            Array.Copy(public_key_bytes, 0, byte0, 1, public_key_bytes.Length);
            var q = curve.Curve.DecodePoint(byte0);

            var keyParameters = new
                    Org.BouncyCastle.Crypto.Parameters.ECPublicKeyParameters(q,
                    domain);

            ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");

            signer.Init(false, keyParameters);
            signer.BlockUpdate(Encoding.UTF8.GetBytes(message), 0, message.Length);

            var signatureBytes = Base58Encoding.Decode(signature);
            var derSign = SignatureHelper.derSign(signatureBytes);
            return signer.VerifySignature(derSign);
        }

        public static string GetSignature(string privateKey, string message, string accountId)
        {
            return GetSignature(privateKey, message);
        }
        public static string GetSignature(string privateKey, string message)
        {
            var curve = SecNamedCurves.GetByName("secp256r1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

            //byte[] pkbytes = Base58Encoding.Decode(privateKey);
            //byte[] pkbytes = Base58Encoding.DecodeWithCheckSum(privateKey);
            byte[] pkbytes = Base58Encoding.DecodePrivateKey(privateKey);

            var keyParameters = new
                    ECPrivateKeyParameters(new BigInteger(1, pkbytes),
                    domain);

            ISigner signer = SignerUtilities.GetSigner("SHA-256withECDSA");

            signer.Init(true, keyParameters);
            signer.BlockUpdate(Encoding.UTF8.GetBytes(message), 0, message.Length);
            var signature = signer.GenerateSignature();
            var netformat = SignatureHelper.ConvertDerToP1393(signature);
            return Base58Encoding.Encode(netformat);
        }

        private static byte[] DerivePublicKeyBytes(string privateKey)
        {
            var curve = SecNamedCurves.GetByName("secp256r1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H);

            byte[] pkbytes = Base58Encoding.DecodePrivateKey(privateKey);
            var d = new BigInteger(1, pkbytes);
            var q = domain.G.Multiply(d);

            var publicKey = new ECPublicKeyParameters(q, domain);

            return publicKey.Q.GetEncoded(false);
        }

        public static string GetAccountIdFromPrivateKey(string privateKey)
        {
            byte[] public_key_bytes = DerivePublicKeyBytes(privateKey);
            return Base58Encoding.EncodeAccountId(public_key_bytes.Skip(1).ToArray());   // skip first byte which indicate compress or not.
        }

        public static string GetPublicKeyFromPrivateKey(string privateKey)
        {
            byte[] public_key_bytes = DerivePublicKeyBytes(privateKey);
            return Base58Encoding.EncodePublicKey(public_key_bytes);
        }

        public static string GeneratePrivateKey()
        {
            var privateKey = new byte[32];
            var rnd = System.Security.Cryptography.RandomNumberGenerator.Create();
            rnd.GetBytes(privateKey);
            return Base58Encoding.EncodePrivateKey(privateKey);
        }

        public static (string privateKey, string AccountId) GenerateWallet(byte[] keyData)
        {
            var pvtKeyStr = Base58Encoding.EncodePrivateKey(keyData);

            var pubKey = GetAccountIdFromPrivateKey(pvtKeyStr);
            return (pvtKeyStr, pubKey);
        }

        public static (string privateKey, string AccountId) GenerateWallet()
        {
            byte[] keyData = new byte[32];
            using (var rnd = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rnd.GetBytes(keyData);
            }
            return GenerateWallet(keyData);
        }
    }
}
