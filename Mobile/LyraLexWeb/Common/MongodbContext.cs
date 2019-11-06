using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LyraLexWeb.Common
{
    public class MongodbContext
    {
        public readonly IMongoDatabase Context;
        private static IMongoDatabase _db;
        public MongodbContext(IOptions<MongodbConfig> configs)
        {
            try
            {
                if(_db == null)
                {
                    var client = new MongoClient(configs.Value.DatabasePath);
                    _db = client.GetDatabase("LexWeb");
                }
                Context = _db;
            }
            catch (Exception ex)
            {
                throw new Exception("Can find or create LiteDb database.", ex);
            }
        }
    }
}
