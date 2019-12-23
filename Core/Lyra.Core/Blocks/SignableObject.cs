using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;

using Lyra.Core.Cryptography;
using System.Threading.Tasks;

namespace Lyra.Core.Blocks
{
    abstract public class SignableObject
    {
        public string Hash { get; set; }

        public string Signature { get; set; }

        public abstract string GetHashInput();

        protected abstract string GetExtraData();

        // Calculate object's SHA256 hash 
        public string CalculateHash()
        {
            string record = GetHashInput();

            using (SHA256Managed sha = new SHA256Managed())
            {
                byte[] hash_bytes = sha.ComputeHash(Encoding.Unicode.GetBytes(record));
                string hash = Base58Encoding.Encode(hash_bytes);
                return hash;
            }
        }

        public async Task<string> SignAsync(ISignatures signer, string PrivateKey)
        {
            if (string.IsNullOrWhiteSpace(Hash))
                Hash = CalculateHash();
            Signature = await signer.GetSignature(PrivateKey, Hash);
            return this.Signature;
        }

        public bool VerifyHash()
        {
            var hash = CalculateHash();
            if (hash != Hash)
                return false;

            return true;
        }

        public async Task<bool> VerifySignatureAsync(ISignatures signer, string PublicKey)
        {
            if (!VerifyHash())
                return false;

            return await signer.VerifyAccountSignature(Hash, PublicKey, Signature);
        }

        public virtual string Print()
        {
            string result = string.Empty;
            //result += $"HashInput: {GetHashInput()}\n";
            result += $"Hash: {Hash}\n";
            result += $"CaclulatedHash: {CalculateHash()}\n";
            result += $"Signature: {Signature}\n";
            return result;
        }

        protected string DateTimeToString(DateTime dateTime)
        {
            return dateTime.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
        }

        public static string CalculateHash(string txt)
        {
            using (SHA256Managed sha = new SHA256Managed())
            {
                byte[] hash_bytes = sha.ComputeHash(Encoding.Unicode.GetBytes(txt));
                string hash = Base58Encoding.Encode(hash_bytes);
                return hash;
            }
        }
    }
}
