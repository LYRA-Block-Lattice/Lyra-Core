using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using Lyra.Data.API.WorkFlow.UniMarket;
using System;

namespace Lyra.Data.API.WorkFlow.UniMarket
{
    public class TOTMeta
    {
        public string name { get; set; } = null!;
        public string description { get; set; } = null!;
        public string image { get; set; } = null!;
        public dynamic? properties { get; set; }
    }
}
