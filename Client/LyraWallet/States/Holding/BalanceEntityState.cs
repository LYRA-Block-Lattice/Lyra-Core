using ReduxSimple.Entity;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.States.Holding
{
    public class BalanceEntityState
        : EntityState<Balance, string>
    {
    }

    public static class Entities
    {
        public static EntityAdapter<Balance, string> BalanceAdaptor
            = EntityAdapter<Balance, string>.Create(item => item.TokenName);
    }
}
