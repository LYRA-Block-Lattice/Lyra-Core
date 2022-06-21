using Lyra.Core.API;
using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow
{
    [LyraWorkFlow]//v
    public class WFOtcOrderCreate : WorkFlowBase
    {
        public static string[] FIATS => new string[]
        {
            "USD", "EUR", "GBP", "CHF", "AUD", "CAD", "JPY", "KRW", "CNY", "TWD", "IDR", "VND", "UAH", "RUB", "THB", "AED"
        };
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_OTC_CRODR,
                RecvVia = BrokerRecvType.DaoRecv,
                Steps = new[]
                {
                    SendTokenFromDaoToOrderAsync,
                    CreateGenesisAsync
                }
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            if (send.Tags.Count != 2 ||
                !send.Tags.ContainsKey("data") ||
                string.IsNullOrWhiteSpace(send.Tags["data"])
                )
                return APIResultCodes.InvalidBlockTags;

            OTCOrder order;
            try
            {
                order = JsonConvert.DeserializeObject<OTCOrder>(send.Tags["data"]);
            }
            catch (Exception ex)
            {
                return APIResultCodes.InvalidBlockTags;
            }

            // daoid
            var dao = await sys.Storage.FindLatestBlockAsync(order.daoId);
            if (string.IsNullOrEmpty(order.daoId) || dao == null || (dao as TransactionBlock).AccountID != send.DestinationAccountId)
                return APIResultCodes.InvalidOrgnization;

            // verify Dealer exists
            var dlr = await sys.Storage.FindLatestBlockAsync(order.dealerId);
            if (string.IsNullOrEmpty(order.dealerId) || dlr == null || dlr is not DealerGenesisBlock)
                return APIResultCodes.InvalidDealerServer;

            // check every field of Order
            // crypto
            var tokenGenesis = await sys.Storage.FindTokenGenesisBlockAsync(null, order.crypto);
            if (tokenGenesis == null)
                return APIResultCodes.TokenNotFound;

            if (order.priceType != PriceType.Fixed)
                return APIResultCodes.InvalidTagParameters;

            // fiat
            if (!FIATS.Contains(order.fiat))
                return APIResultCodes.Unsupported;

            // payBy
            if (order.payBy == null || order.payBy.Length == 0)
                return APIResultCodes.InvalidOrder;

            // price, amount
            if (order.price <= 0.00001m || order.amount < 0.0001m)
                return APIResultCodes.InvalidAmount;

            // verify collateral
            var chgs = send.GetBalanceChanges(last);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE) ||
                chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] < order.collateral)
                return APIResultCodes.InvalidCollateral;

            // verify crypto
            if(order.dir == TradeDirection.Sell)
            {
                if (!chgs.Changes.ContainsKey(order.crypto) ||
                    chgs.Changes[order.crypto] != order.amount ||
                    chgs.Changes.Count != 2)
                    return APIResultCodes.InvalidAmountToSend;
            }
            else
            {
                // buy order
                if (chgs.Changes.Count != 1)
                    return APIResultCodes.InvalidAmountToSend;
            }

            // check the price of order and collateral.
            var dlrblk = await sys.Storage.FindLatestBlockAsync(order.dealerId);
            var uri = new Uri(new Uri((dlrblk as IDealer).Endpoint), "/api/dealer/");
            var dealer = new DealerClient(uri);

            // verify user registered on dealer
            bool userOk = false;
            try
            {
                var user = await dealer.GetUserByAccountIdAsync(send.AccountID);
                if (user.Successful())
                {
                    var stats = JsonConvert.DeserializeObject<UserStats>(user.JsonString);
                    userOk = !string.IsNullOrEmpty(stats.UserName);
                }
            }catch { }

            if (!userOk)
                return APIResultCodes.NotRegisteredToDealer;

            var prices = await dealer.GetPricesAsync();
            var tokenSymbol = order.crypto.Split('/')[1];

            if (prices["LYR"] <= 0)
                return APIResultCodes.RequotaNeeded;

            if (order.collateralPrice != prices["LYR"] || order.fiatPrice != prices[order.fiat.ToLower()])
                return APIResultCodes.PriceChanged;

            if (order.collateralPrice != prices["LYR"])
                return APIResultCodes.PriceChanged;

            decimal usdprice = 0;
            if (tokenSymbol == "ETH") usdprice = prices["ETH"];
            if (tokenSymbol == "BTC") usdprice = prices["BTC"];
            if (tokenSymbol == "USDT") usdprice = prices["USDT"];
            var selcryptoprice = Math.Round(usdprice / prices[order.fiat.ToLower()], 2);

            if (order.collateral * prices["LYR"] < prices[tokenSymbol] * order.amount * ((dao as IDao).SellerPar / 100))
                return APIResultCodes.CollateralNotEnough;

            // dir, priceType
            var total = order.price * order.amount;
            // limit
            if (order.limitMin <= 0 || order.limitMax < order.limitMin
                || order.limitMax > total)
                return APIResultCodes.InvalidArgument;

            return APIResultCodes.Success;
        }

        async Task<TransactionBlock> SendTokenFromDaoToOrderAsync(DagSystem sys, SendTransferBlock send)
        {
            var order = JsonConvert.DeserializeObject<OTCOrder>(send.Tags["data"]);

            var lastblock = await sys.Storage.FindLatestBlockAsync(order.daoId) as TransactionBlock;

            var keyStr = $"{send.Hash.Substring(0, 16)},{order.crypto},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var sendToOrderBlock = new DaoSendBlock
            {
                // block
                ServiceHash = sb.Hash,

                // trans
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                AccountID = lastblock.AccountID,

                // send
                DestinationAccountId = AccountId,

                // broker
                Name = ((IBrokerAccount)lastblock).Name,
                OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId,
                RelatedTx = send.Hash,

                // profiting
                PType = ((IProfiting)lastblock).PType,
                ShareRito = ((IProfiting)lastblock).ShareRito,
                Seats = ((IProfiting)lastblock).Seats,

                // dao
                SellerFeeRatio = ((IDao)lastblock).SellerFeeRatio,
                BuyerFeeRatio = ((IDao)lastblock).BuyerFeeRatio,
                SellerPar = ((IDao)lastblock).SellerPar,
                BuyerPar = ((IDao)lastblock).BuyerPar,
                Description = ((IDao)lastblock).Description,
                Treasure = ((IDao)lastblock).Treasure.ToDecimalDict().ToLongDict(),
            };
            
            // calculate balance
            var dict = lastblock.Balances.ToDecimalDict();

            if(order.dir == TradeDirection.Sell)
                dict[order.crypto] -= order.amount;

            dict[LyraGlobal.OFFICIALTICKERCODE] -= 2;   // for delist and close use later
            sendToOrderBlock.Balances = dict.ToLongDict();

            sendToOrderBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            sendToOrderBlock.InitializeBlock(lastblock, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return sendToOrderBlock;
        }

        async Task<TransactionBlock> CreateGenesisAsync(DagSystem sys, SendTransferBlock send)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);

            var order = JsonConvert.DeserializeObject<OTCOrder>(send.Tags["data"]);

            var keyStr = $"{send.Hash.Substring(0, 16)},{order.crypto},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var otcblock = new OTCOrderGenesisBlock
            {
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // transaction
                AccountType = AccountTypes.OTC,
                AccountID = AccountId,
                Balances = new Dictionary<string, long>(),

                // recv
                SourceHash = (blocks.Last() as TransactionBlock).Hash,

                // broker
                Name = "no name",
                OwnerAccountId = send.AccountID,
                RelatedTx = send.Hash,

                // otc
                Order = order,
                OOStatus = OTCOrderStatus.Open,
            };

            if(order.dir == TradeDirection.Sell)
                otcblock.Balances.Add(order.crypto, order.amount.ToBalanceLong());

            otcblock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, 2m.ToBalanceLong());   // for delist and close use later

            otcblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // pool blocks are service block so all service block signed by leader node
            otcblock.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return otcblock;
        }
    }
}
