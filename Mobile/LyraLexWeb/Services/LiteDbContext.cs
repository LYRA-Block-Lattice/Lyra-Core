using LiteDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace LyraLexWeb.Common
{
    public class LiteDbContext
    {
        public readonly LiteDatabase Context;
        private static LiteDatabase _db;
        public LiteDbContext(IOptions<LiteDbConfig> configs)
        {
            try
            {
                if(_db == null)
                {
                    _db = new LiteDatabase(configs.Value.DatabasePath);
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
