using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Orleans;

namespace Lyra.Core.Cryptography
{
    public interface ISignatures
    {
        Task<bool> VerifyAccountSignature(string message, string accountId, string signature);
        Task<bool> VerifyAuthorizerSignature(string message, string publicKey, string signature);

        Task<string> GetSignature(string privateKey, string message);
        Task<string> GetAccountIdFromPrivateKey(string privateKey);
        Task<string> GetPublicKeyFromPrivateKey(string privateKey);
        Task<(string privateKey, string publicKey)> GenerateWallet();
    }

    public interface ISignaturesForGrain : ISignatures, IGrainWithIntegerKey
    {

    }
}
