using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;

using Lyra.Core.Cryptography;
using Lyra.Core.Protos;

namespace Lyra.Core.Blocks
{
    public abstract class Block: SignableObject
    {
        //public int Id { get; set; }

        public Guid Id { get; set; }

        public int Index { get; set; }

        public DateTime TimeStamp { get; set; }

        public int Version { get; set; }

        /// <summary>
        /// Examples: testnet, mainnet, shopify, etc.
        /// </summary>
        public string NetworkId { get; set; }

        /// <summary>
        /// Examples: US, Europe, Privacy, Exchange, etc.
        /// </summary>
        public string ShardId { get; set; }

        public BlockTypes BlockType { get; set; }

        public string PreviousHash { get; set; }

        // The hash of the most recent service chain block 
        public string ServiceHash { get; set; }

        /// <summary>
        /// Custom metadata in key/value format.
        /// </summary>
        // TO DO there should be additional fee for using Tags based on size in bytes.
        public Dictionary<string, string> Tags { get; set; }

        public virtual BlockTypes GetBlockType() { return BlockTypes.Null; }

        public List<AuthorizationSignature> Authorizations { get; set; }

        public virtual void InitializeBlock(Block prevBlock, string PrivateKey, string NetworkId, string ShardId = "Primary")
        {
            if (prevBlock != null)
            {
                Index = prevBlock.Index + 1;
                PreviousHash = prevBlock.Hash;

            }
            else
            {
                Index = 1;
                PreviousHash = null;//string.Empty;
            }
            this.NetworkId = NetworkId;
            this.ShardId = ShardId;
            TimeStamp = DateTime.Now;
            Version = 1; // to do: change to global constant; should be used to fork the network; should be validated by comparing with the Node Version (taken from teh same globla contstant)
            BlockType = GetBlockType();
            //Hash = CalculateHash();
            Sign(PrivateKey);
        }

        public override string GetHashInput()
        {
            return this.Index.ToString() + "|" +
                             //this.TimeStamp.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                             //this.TimeStamp.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'") +
                             DateTimeToString(TimeStamp) + "|" +
                             this.Version + "|" +
                             this.NetworkId + "|" +
                             this.ShardId + "|" +
                             this.BlockType.ToString() + "|" +
                             this.PreviousHash + "|" +
                             JsonConvert.SerializeObject(Tags) + "|" +
                             this.GetExtraData();
        }

        // should be overriden in specific instance to get the correct hash claculated from the entire block data 
        protected override string GetExtraData()
        {
            return string.Empty;
        }

        // Check if the block is valid by comparing its parameters with the onces from the previous block
        public virtual bool IsBlockValid(Block prevBlock)
        {
            // *** All blocks except for account opening ones must have a previous block
            if (!(this is IOpeningBlock))
            {
                if (string.IsNullOrWhiteSpace(this.PreviousHash))
                    return false;

                if (prevBlock == null)
                    return false;

                if (prevBlock.Index + 1 != this.Index)
                    return false;

                if (prevBlock.Hash != this.PreviousHash)
                    return false;
            }
            else
            {
                if (this.Index != 1) // always 1 for open block
                    return false;
            }

            if (string.IsNullOrWhiteSpace(this.NetworkId))
                return false;

            if (string.IsNullOrWhiteSpace(this.ShardId))
                return false;

            if (!ValidateTags())
                return false;

            //if (!VerifyHash())
            //    return false;

            return true;
        }

        protected const int MAX_TAGS_COUNT = 16;

        protected const int MAX_STRING_LENGTH = 256;

        protected virtual bool ValidateTags()
        {
            if (Tags == null)
                return true;

            if (Tags.Count > MAX_TAGS_COUNT)
                throw new ApplicationException("Too many tags");

            foreach (var tag in Tags)
            {
                if (string.IsNullOrEmpty(tag.Value) && tag.Value.Length > MAX_STRING_LENGTH)
                    throw new ApplicationException("Tag value is too long");

                if (string.IsNullOrEmpty(tag.Key) && tag.Key.Length > MAX_STRING_LENGTH)
                    throw new ApplicationException("Tag key is too long");
            }

            return true;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"Id: {Id.ToString()}\n";
            result += $"Index: {Index.ToString()}\n";
            result += $"TimeStamp: {DateTimeToString(TimeStamp)}\n"; 
            result += $"Version: {Version}\n";
            result += $"NetworkId: {NetworkId}\n";
            result += $"ShardId: {ShardId}\n";
            result += $"BlockType: {BlockType.ToString()}\n";
            result += $"PreviousHash: {PreviousHash}\n";
            result += $"ServiceHash: {ServiceHash}\n";
            result += $"Tags: {JsonConvert.SerializeObject(Tags)}\n";
            result += $"Authorizations: {JsonConvert.SerializeObject(Authorizations)}\n";
            return result;
        }

    }

}
