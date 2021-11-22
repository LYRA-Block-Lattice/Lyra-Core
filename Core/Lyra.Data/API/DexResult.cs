using System.Collections.Generic;

namespace DexServer.Ext
{
    public class DexResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }

    public class SupportedTokens : DexResult
    {
        public List<ExtAssert> Asserts { get; set; }
    }

    public class ExtAssert
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Symbol { get; set; }

        public string NetworkProvider { get; set; }
        public string Contract { get; set; }

        public decimal MinDeposit { get; set; }
        public decimal DepositFee { get; set; }
        public string ConfirmationInfo { get; set; }

        public decimal MinWithdraw { get; set; }
        public decimal WithdrawFee { get; set; }
        public decimal DailyWithdrawLimit { get; set; }
    }

    public class DexAddress : DexResult
    {
        public string Owner { get; set; }
        public string Address { get; set; }
        public string Blockchain { get; set; }
        public string Provider { get; set; }
        public string Network { get; set; }
        public string Contract { get; set; }
    }
}
