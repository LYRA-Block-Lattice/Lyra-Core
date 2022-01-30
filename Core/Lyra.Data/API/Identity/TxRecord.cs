using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Newtonsoft.Json;

namespace Lyra.Data.API.Identity
{
    public enum MessageTypes { Null, Text, Image }

    public abstract class TxRecord : SignableObject
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // reference TxGroup
        public string GroupId { get; set; } = null!;

        // reference LyraUser
        public string UserId { get; set; } = null!;

        public long Height { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonRepresentation(BsonType.Document)]
        public DateTime TimeStamp { get; set; }

        public int Version { get; set; }

        public virtual MessageTypes MessageType { get; set; }

        public string PreviousHash { get; set; }
        
        public TxRecord()
        {
            TimeStamp = DateTime.UtcNow;
        }

        public void Initialize(TxRecord prevRecord, string PrivateKey, string AccountId)
        {
            if (prevRecord != null)
            {
                Height = prevRecord.Height + 1;
                PreviousHash = prevRecord.Hash;

                if (prevRecord.Hash != prevRecord.CalculateHash())
                    throw new Exception("Invalid previous TxMessage, possible data tampered.");
            }
            else
            {
                Height = 1;
                PreviousHash = null;//string.Empty;
            }
            Version = LyraGlobal.DatabaseVersion; // to do: change to global constant; should be used to fork the network; should be validated by comparing with the Node Version (taken from teh same globla contstant)

            Sign(PrivateKey, AccountId);
        }

        public async Task InitializeAsync(TxRecord prevRecord, Func<string, Task<string>> signr)
        {
            if (prevRecord != null)
            {
                Height = prevRecord.Height + 1;
                PreviousHash = prevRecord.Hash;

                if (prevRecord.Hash != prevRecord.CalculateHash())
                    throw new Exception("Invalid previous TxMessage, possible data tampered.");
            }
            else
            {
                Height = 1;
                PreviousHash = null;//string.Empty;
            }
            Version = LyraGlobal.DatabaseVersion; // to do: change to global constant; should be used to fork the network; should be validated by comparing with the Node Version (taken from teh same globla contstant)

            if (string.IsNullOrWhiteSpace(Hash))
                Hash = CalculateHash();
            Signature = await signr(Hash);
        }

        public override string GetHashInput()
        {
            return Height.ToString() + "|" +
                             DateTimeToString(TimeStamp) + "|" +
                             this.Version + "|" +
                             this.MessageType.ToString() + "|" +
                             this.PreviousHash + "|" +
                             this.GetExtraData();
        }

        // should be overriden in specific instance to get the correct hash claculated from the entire TxMessage data 
        protected override string GetExtraData()
        {
            return string.Empty;
        }

        public override string Print()
        {
            string result = base.Print();
            result += $"MessageType: {MessageType}\n";
            result += $"Version: {Version}\n";
            result += $"Height: {Height}\n";
            result += $"TimeStamp: {DateTimeToString(TimeStamp)}\n"; 
            result += $"PreviousHash: {PreviousHash}\n";
            return result;
        }
    }
}
