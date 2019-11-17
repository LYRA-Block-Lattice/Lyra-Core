using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Exchange
{
    public class LyraToken
    {
        [BsonId] 
        public ObjectId Id { get; set; }
        public string Name { get; set; }
        public string NetworkID { get; set; }
    }
}
