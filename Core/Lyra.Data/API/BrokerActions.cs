﻿
namespace Lyra.Data.API
{
    public class BrokerActions
    {
        public const string BRK_POOL_CRPL = "CRPL";
        public const string BRK_POOL_ADDLQ = "ADDLQ";
        public const string BRK_POOL_RMLQ = "RMLQ";
        public const string BRK_POOL_SWAP = "SWAP";

        public const string BRK_STK_CRSTK = "CRSTK";
        public const string BRK_STK_ADDSTK = "ADDSTK";
        public const string BRK_STK_UNSTK = "UNSTK";

        public const string BRK_PFT_CRPFT = "CRPFT";
        //public const string BRK_PFT_FEEPFT = "FEEPFT";    // combine to getpft
        public const string BRK_PFT_GETPFT = "GETPFT";

        public const string BRK_MCT_CRMCT = "CRMCT";
        public const string BRK_MCT_PAYMCT = "PAYMCT";
        public const string BRK_MCT_UNPAY = "UNPAY";
        public const string BRK_MCT_CFPAY = "CFPAY";
        public const string BRK_MCT_GETPAY = "GETPAY";

        // DEX
        public const string BRK_DEX_DPOREQ = "DPOREQ";
        public const string BRK_DEX_MINT = "MINT";
        public const string BRK_DEX_GETTKN = "GETTKN";
        public const string BRK_DEX_PUTTKN = "PUTTKN";
        public const string BRK_DEX_WDWREQ = "WDWREQ";

        // DAO
        public const string BRK_DAO_CRDAO = "CRDAO";

        // OTC
        public const string BRK_OTC_CRODR = "OTCRODR";
    }
}
