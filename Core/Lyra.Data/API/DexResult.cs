using System;
using System.Collections.Generic;

namespace DexServer.Ext
{
    public class DexResult
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
    }

    public class SupportedTokens : DexResult
    {
        public List<ExtAssert> Asserts { get; set; } = null!;
    }

    public class ExtAssert
    {
        public string Name { get; set; } = null!;
        public string CoinGeckoName { get; set; } = null!;  // to get stats from coingecko
        public string Url { get; set; } = null!;
        public string Symbol { get; set; } = null!;

        public string NetworkProvider { get; set; } = null!;
        public string? Contract { get; set; }

        public decimal MinDeposit { get; set; }
        public decimal DepositFee { get; set; }
        public string? ConfirmationInfo { get; set; }

        public decimal MinWithdraw { get; set; }
        public decimal WithdrawFee { get; set; }
        public decimal DailyWithdrawLimit { get; set; }
    }

    public class DexAddress : DexResult
    {
        public string Owner { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string Blockchain { get; set; } = null!;
        public string Provider { get; set; } = null!;
        public string Network { get; set; } = null!;
        public string? Contract { get; set; }
    }

    public class DexHistory : DexResult
    {
        public List<DexTx> Txes { get; set; } = null!;
    }

    public class DexTx
    {
        public DateTime time { get; set; }
        public string action { get; set; } = null!;

        public string owner { get; set; } = null!;
        public string symbol { get; set; } = null!;
        public string provider { get; set; } = null!;
        public string address { get; set; } = null!;

        public decimal amount { get; set; }
    }
}
