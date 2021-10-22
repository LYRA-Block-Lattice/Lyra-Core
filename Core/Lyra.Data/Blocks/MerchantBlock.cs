using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.Blocks
{
    public interface IMerchant : IBrokerAccount
    {

    }
    public class MerchantRecv : BrokerAccountRecv, IMerchant
    {

    }

    public class MerchantSend : BrokerAccountSend, IMerchant
    {

    }
}
