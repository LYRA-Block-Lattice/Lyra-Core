using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.Blocks
{
    public interface IContractBlock
    {
        public string ctype { get; set; }
        public object payload { get; set; }
    }
    public class ContractBlock : IContractBlock
    {
        public string ctype { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public object payload { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    }
}
