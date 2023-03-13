using Lyra.Core.API;
using Lyra.Core.Blocks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.WorkFlow.UniMarket
{
    public class UniTradeFees
    {
        public static (decimal sellerFee, decimal buyerFee, decimal sellerNetworkFee, decimal buyerNetworkFee) CalculateFees(
            IDao dao, UniTrade trade
            )
        {
            // buyer fee calculated as LYR
            var buyerFee = Math.Round(dao.BuyerFeeRatio * trade.cltamt / (dao.BuyerPar / 100), 8);
            var sellerFee = Math.Round(dao.SellerFeeRatio * trade.cltamt / (dao.SellerPar / 100), 8);
            var buyerNetworkFee = Math.Round(LyraGlobal.BidingNetworkFeeRatio * trade.cltamt / (dao.BuyerPar / 100), 8);
            var sellerNetworkFee = Math.Round(LyraGlobal.OfferingNetworkFeeRatio * trade.cltamt / (dao.SellerPar / 100), 8);

            return (sellerFee, buyerFee, sellerNetworkFee, buyerNetworkFee);
        }

        public static (decimal tradeFee, decimal networkFee) CalculateSellerFees(
            decimal eqprice, decimal amount, decimal rito
            )
        {
            return (
                eqprice * amount * rito,
                eqprice * amount * LyraGlobal.OfferingNetworkFeeRatio
                );
        }

        public static (decimal tradeFee, decimal networkFee) CalculateBuyerFees(
            decimal eqprice, decimal amount, decimal rito
            )
        {
            return (
                eqprice * amount * rito,
                eqprice * amount * LyraGlobal.BidingNetworkFeeRatio
                );
        }
    }
}
