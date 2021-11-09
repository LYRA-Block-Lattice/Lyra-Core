﻿using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class ReceiveAsFeeAuthorizer : ReceiveTransferAuthorizer
    {
        protected override AuthorizationFeeTypes GetFeeType()
        {
            return AuthorizationFeeTypes.FullFee;
        }
    }
}
