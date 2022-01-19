using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.IO;
using Lyra.Data.Crypto;
using Lyra.Data.Blocks;

namespace Lyra.Core.Blocks
{
    abstract public class SignableObject : IEquatable<SignableObject>
    {
        public string Hash { get; set; }

        public string Signature { get; set; }

        public abstract string GetHashInput();

        protected abstract string GetExtraData();

        // Calculate object's SHA256 hash 
        public string CalculateHash()
        {
            string record = GetHashInput();
            //if(this is BenefitingBlock)
            //    Console.WriteLine($"Debug Hash Record: {record}");
            using (var sha = SHA256.Create())
            {
                byte[] hash_bytes = sha.ComputeHash(Encoding.Unicode.GetBytes(record));
                string hash = Base58Encoding.Encode(hash_bytes);

                //// debug only, temp code
                //if (record.Contains("LyraTokenGenesis"))
                //    Console.WriteLine($"Hash input: {record}\n Hash: {hash}");
                File.AppendAllText(@"c:\tmp\hash.txt", $"{hash} {record}\n");

                return hash;
            }
        }

        public string Sign(string PrivateKey, string accountId)
        {
            if (string.IsNullOrWhiteSpace(Hash))
                Hash = CalculateHash();
            //Console.WriteLine($"Hash: {Hash}\nAccountID: {accountId}");
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

        public virtual bool VerifySignature(string PublicKey)
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

        public static string DateTimeToString(DateTime dateTime)
        {
            return dateTime.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'");
        }

        public static string CalculateHash(string txt)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash_bytes = sha.ComputeHash(Encoding.Unicode.GetBytes(txt));
                string hash = Base58Encoding.Encode(hash_bytes);
                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SignableObject);
        }

        public bool Equals(SignableObject other)
        {
            return other != null &&
                   Hash == other.Hash;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Hash);
        }

        public static bool operator ==(SignableObject left, SignableObject right)
        {
            return EqualityComparer<SignableObject>.Default.Equals(left, right);
        }

        public static bool operator !=(SignableObject left, SignableObject right)
        {
            return !(left == right);
        }
    }
}
