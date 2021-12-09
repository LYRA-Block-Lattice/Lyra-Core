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
    public interface ILyraSignature
    {
        bool ValidateAccountId(string AccountId);
        bool ValidatePublicKey(string PublicKey);

        bool ValidatePrivateKey(string PrivateKey);

        bool VerifyAccountSignature(string message, string accountId, string signature);

        string GetSignature(string privateKey, string message, string AccountId);

        (string privateKey, string AccountId) GenerateWallet(byte[] keyData);

        (string privateKey, string AccountId) GenerateWallet();

        string GetAccountIdFromPrivateKey(string privateKey);
    }

    public class DotnetSignature : ILyraSignature
    {
        public (string privateKey, string AccountId) GenerateWallet(byte[] keyData)
        {
            return NativeSignatures.GenerateWallet(keyData);
        }

        public (string privateKey, string AccountId) GenerateWallet()
        {
            return NativeSignatures.GenerateWallet();
        }

        public string GetAccountIdFromPrivateKey(string privateKey)
        {
            return NativeSignatures.GetAccountIdFromPrivateKey(privateKey);
        }

        public string GetSignature(string privateKey, string message, string AccountId)
        {
            return NativeSignatures.GetSignature(privateKey, message, AccountId);
        }

        public bool ValidateAccountId(string AccountId)
        {
            return NativeSignatures.ValidateAccountId(AccountId);
        }

        public bool ValidatePrivateKey(string PrivateKey)
        {
            return NativeSignatures.ValidatePrivateKey(PrivateKey);
        }

        public bool ValidatePublicKey(string PublicKey)
        {
            return NativeSignatures.ValidatePublicKey(PublicKey);
        }

        public bool VerifyAccountSignature(string message, string accountId, string signature)
        {
            return NativeSignatures.VerifyAccountSignature(message, accountId, signature);
        }
    }

    public class BCSignature : ILyraSignature
    {
        public (string privateKey, string AccountId) GenerateWallet(byte[] keyData)
        {
            return PortableSignatures.GenerateWallet(keyData);
        }

        public (string privateKey, string AccountId) GenerateWallet()
        {
            return PortableSignatures.GenerateWallet();
        }

        public string GetAccountIdFromPrivateKey(string privateKey)
        {
            return PortableSignatures.GetAccountIdFromPrivateKey(privateKey);
        }

        public string GetSignature(string privateKey, string message, string AccountId)
        {
            return PortableSignatures.GetSignature(privateKey, message, AccountId);
        }

        public bool ValidateAccountId(string AccountId)
        {
            return PortableSignatures.ValidateAccountId(AccountId);
        }

        public bool ValidatePrivateKey(string PrivateKey)
        {
            return PortableSignatures.ValidatePrivateKey(PrivateKey);
        }

        public bool ValidatePublicKey(string PublicKey)
        {
            return PortableSignatures.ValidatePublicKey(PublicKey);
        }

        public bool VerifyAccountSignature(string message, string accountId, string signature)
        {
            return PortableSignatures.VerifyAccountSignature(message, accountId, signature);
        }
    }
}
