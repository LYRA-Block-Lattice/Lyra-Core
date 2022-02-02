using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.Identity
{
    /// <summary>
    /// SignalR group for the transaction. Uniqe for every transacton.
    /// </summary>
    public class TxRoom
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string TradeId { get; set; } = null!;

        // add full user document as a snapshot
        public LyraUser[] Members { get; set; }
    }
}
