using FluentAssertions;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Data.Crypto;
using Lyra.Core.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UnitTests
{
    [TestClass]
    public class UT_Cryptography
    {
        const string PrivateKey1 = "26qLSmxsAe1YYtHnHCde9749QqzSkY8W7PwyJXUqDaPb6iuedq";
        const string PublicKey1 = "LQnynZRbNMYtNJxVdL5LUJtGX8qCkKpQSDhArNU8Vhgy8eSix2oTu69C7u4WQeH5RDWvVRbDKBKqN3HtCaK6p6t79fHNmy";
        const string PrivateKey2 = "wj22AaVJX3xpB1idUCd93j36K6GtqJvCCqzPwmkj1YhoYT3Tq";
        const string PublicKey2 = "La5UhGXdEdAibQbccAFLhwLupWhvspegfg4pHFP1T5wiXAxDuLi43YcF1Wa3B3wPecP9G2Um4p7jj7F41EJN7ySAJH8Uqn";
        const string message = "Hello, World!";

        [TestMethod]
        [ExpectedException(typeof(System.FormatException))]
        public void BadPrivateKeyFormat()
        {
            var private_key = "1234567890";
            var account_id = Signatures.GetAccountIdFromPrivateKey(private_key);
        }

        [TestMethod]
        public void CorrectPrivateKeyAndAccountFormat()
        {
            
            var account_id = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);
            var result = Signatures.ValidateAccountId(account_id);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SignNeo()
        {
            var account_id = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);
            var result = Signatures.GetSignature(PrivateKey1, message, account_id);
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void VerifySignatureNeo()
        {
            var account_id = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);
            var signature = Signatures.GetSignature(PrivateKey1, message, account_id);

            var result = Signatures.VerifyAccountSignature(message, account_id, signature);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void SignMono()
        {
            var account_id = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);
            var result = PortableSignatures.GetSignature(PrivateKey1, message);
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void AccountIdByNeo()
        {
            var pubKey1 = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);
            Assert.AreEqual(pubKey1, PublicKey1);
        }

        [TestMethod]
        public void AccountIdByMono()
        {
            var pubKey1 = PortableSignatures.GetAccountIdFromPrivateKey(PrivateKey1);
            Assert.AreEqual(pubKey1, PublicKey1);
        }

        [TestMethod]
        public void VerifySignatureMono()
        {
            var account_id = PortableSignatures.GetAccountIdFromPrivateKey(PrivateKey1);
            Assert.AreEqual(account_id, PublicKey1);

            var signature = PortableSignatures.GetSignature(PrivateKey1, message);

            var result = PortableSignatures.VerifyAccountSignature(message, account_id, signature);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyNeoSignatureByMono()
        {
            var account_id = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

            var signature = Signatures.GetSignature(PrivateKey1, message, account_id);

            var result = PortableSignatures.VerifyAccountSignature(message, account_id, signature);
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void VerifyMonoSignatureByNeo()
        {
            var account_id = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);
            Assert.AreEqual(account_id, PublicKey1);
            var signature = PortableSignatures.GetSignature(PrivateKey1, message);

            var result = Signatures.VerifyAccountSignature(message, account_id, signature);
            Assert.IsTrue(result);
        }

        // con't compare signatures directly
        //[TestMethod]
        //public void SignNeoAndMono()
        //{
        //    var account_id = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

        //    var neo = Signatures.GetSignature(PrivateKey1, message, account_id);

        //    var mono = PortableSignatures.GetSignature(PrivateKey1, message);

        //    Assert.AreEqual(neo, mono);
        //}

        [TestMethod]
        public void GetSharedSecret()
        {
            var AccountId2 = Signatures.GetAccountIdFromPrivateKey(PrivateKey2);

            var crypto = new ECC_DHA_AES_Encryptor();
            var result = crypto.GetSharedSecret(PrivateKey1, AccountId2);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public void GetSharedSecretOnBothKeyPairs()
        {
            var AccountId1 = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

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
            var AccountId1 = Signatures.GetAccountIdFromPrivateKey(PrivateKey1);

            var AccountId2 = Signatures.GetAccountIdFromPrivateKey(PrivateKey2);

            var crypto = new ECC_DHA_AES_Encryptor();

            var salt = "54CRv2pEjNj8c3UBPv4AxYmEKEojmVdAagTpgSjHieAq";

            string encryptedMessage = crypto.Encrypt(PrivateKey1, AccountId2, salt, message);
            string decryptedMessage = crypto.Decrypt(PrivateKey2, AccountId1, salt, encryptedMessage);

            Assert.AreEqual<string>(message, decryptedMessage);
        }
    }
}
