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

        public string LyraTxHash { get; set; } = null!;

        // tag -> user id. i.e "seller": aaaaa, "buyer": bbbbb, "dealer": ccccc, etc.
        public Dictionary<string, string> Members { get; set; }
    }
}
