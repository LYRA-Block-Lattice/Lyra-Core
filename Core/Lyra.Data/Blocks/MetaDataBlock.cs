using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.Blocks
{
    /// <summary>
    /// Storage of big chunk of data which not change constantly.
    /// e.g. descriptive data, order, product, image(?), etc.
    /// like store a small png logo (<20KB) for token.
    /// </summary>
    //public abstract class MetaDataBlock : Block
    //{
    //    protected abstract string Serialize();
    //    public override bool AuthCompare(Block other)
    //    {
    //        var ob = other as MetaDataBlock;

    //        return base.AuthCompare(ob) &&
    //            Serialize() == ob.Serialize()
    //            ;
    //    }

    //    protected override string GetExtraData()
    //    {
    //        string extraData = base.GetExtraData();
    //        extraData += Serialize() + "|";
    //        return extraData;
    //    }

    //    public override string Print()
    //    {
    //        string result = base.Print();
    //        return result;
    //    }
    //}

    //public class DaoInfo : MetaDataBlock
    //{
    //    public string Name { get; set; }
    //    public string Description { get; set; }

    //    protected override string Serialize()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public override string Print()
    //    {
    //        return base.Print();
    //    }
    //}
}
