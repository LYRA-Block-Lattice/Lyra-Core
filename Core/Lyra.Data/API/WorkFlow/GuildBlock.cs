using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow
{
    [BsonIgnoreExtraElements]
    public class GuildRecvBlock : DaoRecvBlock
    {
        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.GuildRecv;
        }
    }

    [BsonIgnoreExtraElements]
    public class GuildSendBlock : DaoSendBlock
    {
        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.GuildSend;
        }
    }


    [BsonIgnoreExtraElements]
    public class GuildGenesisBlock:  DaoGenesisBlock
    {
        protected override BlockTypes GetBlockType()
        {
            return BlockTypes.GuildGenesis;
        }
    }
}
