using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.Identity;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests.OTC
{
    [TestClass]
    public class UT_Trade : XOTCTestBase
    {
        public async Task Setup()
        {
            await SetupWallets("devnet");

            await SetupEventsListener();
        }

        [TestMethod]
        public async Task TradeCancelling()
        {
            await Setup();

            var order = await CreateOrder();
            Assert.IsNotNull(order);

            var trade = await CreateTrade(order);
            Assert.IsNotNull(trade);

            await CancelTrade(trade);

            await CloseOrder(order);
        }

        [TestMethod]
        public async Task DisputeRaiseLevel1()
        {
            await Setup();

            var order = await CreateOrder();
            Assert.IsNotNull(order);

            var trade = await CreateTrade(order);
            Assert.IsNotNull(trade);

            var lsb = await client.GetLastServiceBlockAsync();
            var brief0 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(trade.AccountID, brief0.TradeId);
            Assert.AreEqual(DisputeLevels.None, brief0.DisputeLevel);

            var ret = await dealer.ComplainAsync(trade.AccountID, trade.Trade.collateral, test2PublicKey,
                Signatures.GetSignature(test2PrivateKey, lsb.GetBlock().Hash, test2PublicKey)                
                );
            Assert.IsTrue(ret.Successful());

            var brief1 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(trade.AccountID, brief1.TradeId);
            Assert.AreEqual(DisputeLevels.Peer, brief1.DisputeLevel);

            await CancelTradeShouldFail(trade);

            await CloseOrderShouldFail(order);
        }

        /// <summary>
        /// dispute level: DAO
        /// DAO try to provide resolution and both part will accept it.
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async Task ResolveDisputeOnDAO()
        {
            await Setup();

            var tradeid = "L9vZrJwDoV1buVd91SY5JrozphDQqgmxQQTpFzJVvd6PEQ4gQ3ZHCGGykUZ2EJmX6ReuX3dAuWN3MZMHRKV3goWK7UNCZh";
            var trade = (await client.GetLastBlockAsync(tradeid)).As<IOtcTrade>();
            Assert.IsTrue(trade != null && trade.OTStatus == OTCTradeStatus.Dispute);

            var lsb = await client.GetLastServiceBlockAsync();
            var brief0 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(DisputeLevels.DAO, brief0.DisputeLevel);

            // seems dao owner is test1
            var dao = (await client.GetLastBlockAsync(trade.Trade.daoId)).As<IDao>();
            Assert.AreEqual(testPublicKey, dao.OwnerAccountId);

            // dao owner create a resolution
            var resolution = await CreateODRResolution(dao, trade);
            Assert.IsNotNull(resolution);

            // resolution submit to dealer

            // buyer and seller will accept the resolution

            // dao owner execute the resolution
            var ret = await testWallet.ExecuteResolution(null, resolution);
            Assert.IsTrue(ret.Successful(), $"Failed to execute resolution: {ret.ResultCode}");
        }

        private async Task<ODRResolution> CreateODRResolution(IDao dao, IOtcTrade trade)
        {
            TransMove[] moves = new TransMove[1];
            moves[0] = new TransMove
            {
                from = Parties.DAOTreasure,
                to = Parties.Buyer,
                amount = trade.Trade.collateral,
                desc = "return collateral to buyer"
            };

            var resolution = new ODRResolution
            {
                RType = ResolutionType.OTCTrade,
                creator = testWallet.AccountId,
                tradeid = trade.AccountID,
                actions = moves,
            };
            //daoprosl = new VoteProposal
            //{
            //    pptype = ProposalType.DisputeResolution,
            //    data = JsonConvert.SerializeObject(resolution),
            //};

            return resolution;
        }

        private async Task<TradeBrief> GetBrief(string hash, string tradeId)
        {            
            var briefret = await dealer.GetTradeBriefAsync(tradeId, test2PublicKey,
                Signatures.GetSignature(test2PrivateKey, hash, test2PublicKey));
            Assert.IsTrue(briefret.Successful());
            var brief = briefret.Deserialize<TradeBrief>();
            return brief;
        }
    }
}
