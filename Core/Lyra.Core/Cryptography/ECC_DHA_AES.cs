using System.Text;
using System.IO;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Crypto.Agreement.Kdf;
using System.Security.Cryptography;

namespace Lyra.Core.Cryptography
{
    public class ECC_DHA_AES_Encryptor
    {
        public ECC_DHA_AES_Encryptor()
        {
        }

        // Salt, aka Nonce, aka IV - we can use the block hash to avoid using another extra string
        public string Encrypt(string LocalPrivateKey, string RemoteAccountId, string Salt, string PlainText)
        {
            var secret = GetSharedSecret(LocalPrivateKey, RemoteAccountId);

            //var plainTextBytes = Encoding.Unicode.GetBytes(PlainText);
            var saltBytes = Encoding.Unicode.GetBytes(Salt.ToCharArray(), 0, 8);

            using (var rijndael = new RijndaelManaged())
            {
                //rijndael.Key = secret;
                //rijndael.IV = saltBytes;
                var encryptor = rijndael.CreateEncryptor(secret, saltBytes);
                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(cryptoStream))
                        {

                            //Write all data to the stream.
                            swEncrypt.Write(PlainText);
                            //encrypted = msEncrypt.ToArray();
                            //cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);
                            //cryptoStream.FlushFinalBlock();
                            
                        }
                        var cipherTextBytes = memoryStream.ToArray();
                        var cipherText = Base58Encoding.Encode(cipherTextBytes);
                        return cipherText;
                    }
                }
            }
        }

        public string Decrypt(string LocalPrivateKey, string RemoteAccountId, string Salt, string CipherText)
        {
            var secret = GetSharedSecret(LocalPrivateKey, RemoteAccountId);
            var cipherTextBytes = Base58Encoding.Decode(CipherText);
            var saltBytes = Encoding.Unicode.GetBytes(Salt.ToCharArray(), 0, 8);
            using (var rijndael = new RijndaelManaged())
            {
                //rijndael.Key = secret;
                //rijndael.IV = saltBytes;
                var decryptor = rijndael.CreateDecryptor(secret, saltBytes);
                using (var memoryStream = new MemoryStream(cipherTextBytes))
                {
                    using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(cryptoStream))
                        {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            var plainText = srDecrypt.ReadToEnd();

                            //var plainTextBytes = new byte[cipherTextBytes.Length];
                            //int plainTextByteActualSize = cryptoStream.Read(plainTextBytes, 0, plainTextBytes.Length);
                            //var plainText = Base58Encoding.Encode(plainTextBytes);
                            return plainText;
                        }
                    }
                }
            }
        }


        public byte[] GetSharedSecret(string LocalPrivateKey, string RemoteAccountId)
        {
            var curve = SecNamedCurves.GetByName("secp256r1");
            var domain = new ECDomainParameters(curve.Curve, curve.G, curve.N, curve.H, curve.GetSeed());

            //byte[] pkbytes = Base58Encoding.DecodeWithCheckSum(LocalPrivateKey);
            byte[] pkbytes = Base58Encoding.DecodePrivateKey(LocalPrivateKey);

            var privateKeyParameters = new
                    ECPrivateKeyParameters(new BigInteger(pkbytes),
                    domain);

            var dh = new ECDHCBasicAgreement();
            dh.Init(privateKeyParameters);


            //var publicKeyBytes = Base58Encoding.DecodeWithCheckSum(RemotePublicKey);
            var publicKeyBytes = Base58Encoding.DecodeAccountId(RemoteAccountId);

            var q = curve.Curve.DecodePoint(publicKeyBytes);

            var publicKeyParameters = new ECPublicKeyParameters(q, domain);

            var sharedSecret = dh.CalculateAgreement(publicKeyParameters);

            using (SHA256Managed sha = new SHA256Managed())
            {
                var hash = sha.ComputeHash(sharedSecret.ToByteArray());
                return hash;
            }
        }
    }
}
