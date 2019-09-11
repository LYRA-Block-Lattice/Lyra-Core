using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lyra.Core.Cryptography;

namespace Lyra.Node.Test
{
    [TestClass]
    public class CryptographyTest
    {
        public CryptographyTest()
        {
        }

        [TestMethod]
        [ExpectedException(typeof(FormatException))]
        public void PrivateKeyFormat()
        {
            var private_key = "1234567890";
            var account_id = Signatures.GetAccountIdFromPrivateKey(private_key);

            var crypto = new ECC_DHA_AES_Encryptor();
            var result = crypto.GetSharedSecret(private_key, account_id);
        }

        [TestMethod]
        public void GetSharedSecret()
        {
            var PrivateKey1 = "DQDP23xgHmLSsdm64qu1UsMteA5qDfgTiFRQRbjnfKstkg4LN";
            var AccountId1 = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

            var PrivateKey2 = "2CcBpc2vn8uXeiXp7sW15w3wFYCWt36VufrKuPBzqnxVQto64H";
            var AccountId2 = Signatures.GetAccountIdFromPrivateKey(PrivateKey2);

            var crypto = new ECC_DHA_AES_Encryptor();
            var result = crypto.GetSharedSecret(PrivateKey1, AccountId2);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetSharedSecretOnBothKeyPairs()
        {
            var PrivateKey1 = "DQDP23xgHmLSsdm64qu1UsMteA5qDfgTiFRQRbjnfKstkg4LN";
            var AccountId1 = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

            var PrivateKey2 = "2CcBpc2vn8uXeiXp7sW15w3wFYCWt36VufrKuPBzqnxVQto64H";
            var AccountId2 = Signatures.GetAccountIdFromPrivateKey(PrivateKey2);

            var crypto = new ECC_DHA_AES_Encryptor();
            byte[] result1 = crypto.GetSharedSecret(PrivateKey1, AccountId2);
            byte[] result2 = crypto.GetSharedSecret(PrivateKey2, AccountId1);

            string str1 = Base58Encoding.Encode(result1);
            string str2 = Base58Encoding.Encode(result2);

            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);

            Assert.AreEqual<string>(str1, str2);
        }

        [TestMethod]
        public void EncryptionDecryption()
        {
            var PrivateKey1 = "DQDP23xgHmLSsdm64qu1UsMteA5qDfgTiFRQRbjnfKstkg4LN";
            var AccountId1 = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

            var PrivateKey2 = "2CcBpc2vn8uXeiXp7sW15w3wFYCWt36VufrKuPBzqnxVQto64H";
            var AccountId2 = Signatures.GetAccountIdFromPrivateKey(PrivateKey2);

            var crypto = new ECC_DHA_AES_Encryptor();

            var message = "This is the test message";
            var salt = "54CRv2pEjNj8c3UBPv4AxYmEKEojmVdAagTpgSjHieAq";

            string encryptedMessage = crypto.Encrypt(PrivateKey1, AccountId2, salt, message);
            string decryptedMessage = crypto.Decrypt(PrivateKey2, AccountId1, salt, encryptedMessage);

            Assert.AreEqual<string>(message, decryptedMessage);
        }
    }
}
