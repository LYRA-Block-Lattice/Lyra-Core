using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;

using Lyra.Core.Cryptography;
using System.Threading.Tasks;
using System.IO;

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
            // debug
            File.AppendAllText(@"c:\tmp\signer.log", $"  Sign:   {Hash} With: {PrivateKey} Got: {Signature}\n");
            //if(PrivateKey == "2tzpzECRYKQueaCX7d7wFWqHzU2XKHYJ8G8hX56uh44j4N85Q1")
            //{
            //    var result = await VerifySignatureAsync(signer, "L3jk7gUtcXrxNt2GxanwY5ksiJibADdj18EwpbEDmGs2AECfwouggyz6YXBNFYT13xJ8CJNahnGZQWwjKssB6bid1BBgS9F");
            //}
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

            var result = await signer.VerifyAccountSignature(Hash, PublicKey, Signature);
            // debug
            File.AppendAllText(@"c:\tmp\signer.log", $"  Verify: {Hash} against: {Signature} With: {PublicKey} Result: {result}\n");
            return result;
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
