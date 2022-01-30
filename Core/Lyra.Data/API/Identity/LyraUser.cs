using Lyra.Core.Blocks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.Identity
{
    public class LyraUser : SignableObject
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        [BsonRepresentation(BsonType.Document)]
        [BsonElement("RegistedTime")]
        public DateTime RegistedTime { get; set; }

        [BsonElement("FirstName")]
        public string FirstName { get; set; } = null!;

        [BsonElement("LastName")]
        public string LastName { get; set; } = null!;

        [BsonElement("MiddleName")]
        public string? MiddleName { get; set; }

        [BsonElement("Email")]
        public string Email { get; set; } = null!;

        [BsonElement("MobilePhone")]
        public string? MobilePhone { get; set; }

        /// <summary>
        /// lyra wallet public address
        /// </summary>
        [BsonElement("AccountId")]
        public string AccountId { get; set; } = null!;

        /// <summary>
        /// file id of avatar. avatar uploaded to server and get an ID
        /// </summary>
        [BsonElement("AvatarId")]
        public string? AvatarId { get; set; }

        public override string GetHashInput()
        {
            return FirstName + "|" +
                LastName + "|" +
                MiddleName + "|" +
                Email + "|" +
                MobilePhone + "|" +
                AccountId + "|" +

                GetExtraData();
        }

        protected override string GetExtraData()
        {
            return string.Empty;
        }
    }
}
