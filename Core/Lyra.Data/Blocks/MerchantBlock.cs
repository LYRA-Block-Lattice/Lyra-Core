using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.Blocks
{
    public interface IMerchant
    {

    }
    public class MerchantRecv : ReceiveTransferBlock, IBrokerAccount, IMerchant
    {
        public string Name { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }
    }

    public class MerchantSend : SendTransferBlock, IBrokerAccount, IMerchant
    {
        public string Name { get; set; }
        public string OwnerAccountId { get; set; }
        public string RelatedTx { get; set; }
    }
}
