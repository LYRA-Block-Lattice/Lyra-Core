
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

        //public const string BRK_MCT_CRMCT = "CRMCT";
        //public const string BRK_MCT_PAYMCT = "PAYMCT";
        //public const string BRK_MCT_UNPAY = "UNPAY";
        //public const string BRK_MCT_CFPAY = "CFPAY";
        //public const string BRK_MCT_GETPAY = "GETPAY";

        // DEX
        public const string BRK_DEX_DPOREQ = "DPOREQ";
        public const string BRK_DEX_MINT = "MINT";
        public const string BRK_DEX_GETTKN = "GETTKN";
        public const string BRK_DEX_PUTTKN = "PUTTKN";
        public const string BRK_DEX_WDWREQ = "WDWREQ";

        // Fiat
        public const string BRK_FIAT_CRACT = "FATCRACT";
        public const string BRK_FIAT_PRINT = "FATPRNT";
        public const string BRK_FIAT_GET = "FATGET";

        // DAO
        public const string BRK_DAO_CRDAO = "CRDAO";
        public const string BRK_DAO_JOIN = "JOINDAO";
        public const string BRK_DAO_LEAVE = "LEAVEDAO";
        public const string BRK_DAO_CHANGE = "CHANGEDAO";
        public const string BRK_DAO_VOTED_CHANGE = "VTCHGDAO";

        // OTC
        public const string BRK_OTC_CRODR = "CRODR";
        public const string BRK_OTC_CRTRD = "CRTRD";
        public const string BRK_OTC_TRDPAYSENT = "TRDPAYSNT";
        public const string BRK_OTC_TRDPAYGOT = "TRDPAYGOT";
        public const string BRK_OTC_ORDDELST = "ORDDELST";
        public const string BRK_OTC_ORDCLOSE = "ORDCLOSE";
        public const string BRK_OTC_TRDCANCEL = "TRDCANCEL";

        // OTC Dispute
        public const string BRK_OTC_CRDPT = "ORDCRDPT";
        public const string BRK_OTC_RSLDPT = "ORDRSLDPT";

        // Voting
        public const string BRK_VOT_CREATE = "CRVOTS";
        public const string BRK_VOT_VOTE = "VOTE";
        public const string BRK_VOT_CLOSE = "VOTCLS";

        // Dealer
        public const string BRK_DLR_CREATE = "DLRCRT";
        public const string BRK_DLR_UPDATE = "DLRUPD";

        // Universal Order/Trade
        public const string BRK_UNI_CRODR = "UCRODR";
        public const string BRK_UNI_CRTRD = "UCRTRD";
        public const string BRK_UNI_TRDPAYSENT = "UTRDPAYSNT";
        public const string BRK_UNI_TRDPAYGOT = "UTRDPAYGOT";
        public const string BRK_UNI_ORDDELST = "UORDDELST";
        public const string BRK_UNI_ORDCLOSE = "UORDCLOSE";
        public const string BRK_UNI_TRDCANCEL = "UTRDCANCEL";

        // Universal Dispute
        public const string BRK_UNI_CRDPT = "UORDCRDPT";
        public const string BRK_UNI_RSLDPT = "UORDRSLDPT";
    }
}
