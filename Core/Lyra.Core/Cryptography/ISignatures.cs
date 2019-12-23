using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Cryptography
{
    public interface ISignatures
    {
        bool ValidateAccountId(string AccountId);
        bool ValidatePublicKey(string PublicKey);
        bool ValidatePrivateKey(string PrivateKey);
        bool VerifyAccountSignature(string message, string accountId, string signature);
        bool VerifyAuthorizerSignature(string message, string publicKey, string signature);

        string GetSignature(string privateKey, string message);
        string GetAccountIdFromPrivateKey(string privateKey);
        string GetPublicKeyFromPrivateKey(string privateKey);
        (string privateKey, string publicKey) GenerateWallet();
    }
}
