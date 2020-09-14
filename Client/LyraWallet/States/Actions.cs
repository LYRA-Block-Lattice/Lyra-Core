using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.States
{
    public class WalletErrorAction
    {
        public Exception Error { get; set; }
    }

    public class WalletOpenAction {
        public string path { get; set; }
        public string name { get; set; }
        public string password { get; set; }
    }
    public class WalletCreateAction { 
        public string path { get; set; }
        public string network { get; set; }
        public string name { get; set; }
        public string password { get; set; }
    }

    public class WalletRestoreAction { 
        public string privateKey { get; set; }
        public string path { get; set; }
        public string network { get; set; }
        public string name { get; set; }
        public string password { get; set; }
    }

    public class WalletOpenResultAction
    {
        public Wallet wallet { get; set; }
    }
    
    public class WalletRemoveAction {
        public string path { get; set; }
        public string name { get; set; }
    }

    public class WalletChangeVoteAction
    {
        public Wallet wallet { get; set; }
        public string VoteFor { get; set; }
    }

    public class WalletRefreshBalanceAction { public Wallet wallet { get; set; } }

    public class WalletTransactionResultAction
    {
        public Wallet wallet { get; set; }
        public string txName { get; set; }
        public APIResult txResult { get; set; }
    }

    public class WalletSendTokenAction
    {
        public Wallet wallet { get; set; }
        public string DstAddr { get; set; }
        public string TokenName { get; set; }
        public decimal Amount { get; set; }
    }

    public class WalletCreateTokenAction
    {
        public Wallet wallet { get; set; }
        public string tokenName { get; set; }
        public string tokenDomain { get; set; }
        public string description { get; set; }
        public decimal totalSupply { get; set; }
        public int precision { get; set; }
        public string ownerName { get; set; }
        public string ownerAddress { get; set; }
    }

    public class WalletImportAction
    {
        public Wallet wallet { get; set; }
        public string targetPrivateKey { get; set; }
    }

    public class WalletRedeemAction
    {
        public Wallet wallet { get; set; }
        public string tokenToRedeem { get; set; }
        public int countToRedeem { get; set; }
    }

    public class WalletNonFungibleTokenAction
    {
        public Wallet wallet { get; set; }
        public NonFungibleToken nfToken { get; set; }
    }

    public class WalletNonFungibleTokenResultAction
    {
        public Wallet wallet { get; set; }
        public string name { get; set; }
        public decimal denomination { get; set; }
        public string redemptionCode { get; set; }
    }

    public class GetApiVersionAction
    {
        public string Platform { get; set; }
        public string Network { get; set; }
        public string AppName { get; set; }
        public string AppVersion { get; set; }
    }

    public class GetApiVersionSuccessAction
    {
        public bool UpgradeNeeded { get; set; }
        public bool MustUpgradeToConnect { get; set; }
    }

    public class GetApiVersionFailedAction
    {
        public Exception Error { get; set; }
    }
}
