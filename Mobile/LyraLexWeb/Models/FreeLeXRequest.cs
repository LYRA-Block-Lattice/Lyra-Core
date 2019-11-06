using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace LyraLexWeb.Models
{
    public class FreeLeXRequest
    {
        [BsonId]
        public ObjectId ID { get; set; }
        public int State { get; set; }
        public DateTime TimeRequested => DateTime.Now;
        public DateTime SentTime;
        public string UserName { get; set; }
        public string Email { get; set; }
        [Required]
        [MinLength(95), MaxLength(95)]
        public string AccountID { get; set; }
    }
}
