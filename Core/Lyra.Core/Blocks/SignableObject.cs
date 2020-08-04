using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;

using Lyra.Core.Cryptography;
using System.Threading.Tasks;
using System.IO;
using LiteDB;

namespace Lyra.Core.Blocks
{
    abstract public class SignableObject
    {
        [BsonId]
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

        public string Sign(string PrivateKey, string accountId)
        {
            if (string.IsNullOrWhiteSpace(Hash))
                Hash = CalculateHash();
            Signature = Signatures.GetSignature(PrivateKey, Hash, accountId);
            return this.Signature;
        }

        public bool VerifyHash()
        {
            var hash = CalculateHash();
            if (hash != Hash)
                return false;

            return true;
        }

        public bool VerifySignature(string PublicKey)
        {
            if (!VerifyHash())
                return false;

            var result = Signatures.VerifyAccountSignature(Hash, PublicKey, Signature);
            return result;
        }

        public virtual string Print()
        {
            string result = string.Empty;
            //result += $"HashInput: {GetHashInput()}\n";
            result += $"Hash: {Hash}\n";
            //result += $"CalculatedHash: {CalculateHash()}\n";
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
