using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.Identity;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson.Serialization.IdGenerators;
using Newtonsoft.Json;
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
        public async Task GuestTradeCancelling()
        {
            await Setup();

            var order = await HostCreateOrder();
            Assert.IsNotNull(order);

            var trade = await GuestCreateTrade(order);
            Assert.IsNotNull(trade);

            await GuestCancelTradeAsync(trade);

            await HostCloseOrder(order);
        }

        //[TestMethod]
        public async Task DisputeRaiseLevel1()
        {
            await Setup();

            var order = await HostCreateOrder();
            Assert.IsNotNull(order);

            var trade = await GuestCreateTrade(order);
            Assert.IsNotNull(trade);

            var lsb = await client.GetLastServiceBlockAsync();
            var brief0 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(trade.AccountID, brief0.TradeId);
            Assert.AreEqual(DisputeLevels.None, brief0.DisputeLevel);
            Assert.AreEqual(null, brief0.DisputeHistory);
            Assert.AreEqual(null, brief0.ResolutionHistory);

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

        [TestMethod]
        public async Task ResolveDisputeOnPeerSuccess()
        {
            await ResolveGuestComplainByHost(true);
        }

        [TestMethod]
        public async Task ResolveDisputeOnPeerFailedSuccessOnDAO()
        {
            var trade = await ResolveGuestComplainByHost(false);

            // guest complain again to raise the dispute level
            await GuestComplainLevel1(trade);

            var resolution = await CreateODRResolution(trade);
            Assert.IsNotNull(resolution);

            // dao owner need to create a vote
            var vote = await DaoOwnerCreateAVote(trade, resolution);

            // test3 as staker vote yay
            var voteRet2 = await test3Wallet.Vote(vote.AccountID, 1);
            Assert.IsTrue(voteRet2.Successful(), $"Vote error: {voteRet2.ResultCode}");

            await Task.Delay(2000);

            // after voting is decided, there will be a time delay to execute. such as one day.
            // then the DAO could execute the resolution. in the time delay, anyone can raise the
            // dispute to Lyra council level via 1) reason 2) payment of judgement fee.

            // dao owner execute the resolution
            var ret = await test4Wallet.ExecuteResolution(vote.AccountID, resolution);
            Assert.IsTrue(ret.Successful(), $"Failed to execute resolution: {ret.ResultCode}");
        }

        [TestMethod]
        public async Task ResolveDisputeOnPeerFailedAndFailedOnDAO()
        {
            var trade = await ResolveGuestComplainByHost(false);

            // guest complain again to raise the dispute level
            await GuestComplainLevel1(trade);

            var resolution = await CreateODRResolution(trade);
            Assert.IsNotNull(resolution);

            // dao owner need to create a vote
            var vote = await DaoOwnerCreateAVote(trade, resolution);

            // test3 as staker vote yay
            var voteRet2 = await test3Wallet.Vote(vote.AccountID, 1);
            Assert.IsTrue(voteRet2.Successful(), $"Vote error: {voteRet2.ResultCode}");

            await Task.Delay(2000);

            // after voting is decided, there will be a time delay to execute. such as one day.
            // then the DAO could execute the resolution. in the time delay, anyone can raise the
            // dispute to Lyra council level via 1) reason 2) payment of judgement fee.

            // the guest is not willing to accept the decision
            var lsb = await client.GetLastServiceBlockAsync();
            var ret = await dealer.RaiseToCouncilAsync(trade.AccountID, test2PublicKey,
            Signatures.GetSignature(test2PrivateKey, lsb.GetBlock().Hash, test2PublicKey)
            );
            Assert.IsTrue(ret.Successful());

            // dao owner execute the resolution
            var retex = await test4Wallet.ExecuteResolution(vote.AccountID, resolution);
            Assert.IsTrue(!retex.Successful(), $"Should failed to execute resolution: {retex.ResultCode}");
        }

        /// <summary>
        /// dispute level: Peer
        /// Buyer raise complain and seller will accept it.
        /// </summary>
        /// <returns></returns>
        public async Task<IOtcTrade> ResolveGuestComplainByHost(bool accepted)
        {
            await Setup();

            var order = await HostCreateOrder();
            Assert.IsNotNull(order);

            var trade = await GuestCreateTrade(order);
            Assert.IsNotNull(trade);

            var lsb = await client.GetLastServiceBlockAsync();
            await GuestComplainLevel0(trade);

            var brief00 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(DisputeLevels.Peer, brief00.DisputeLevel);

            Assert.AreEqual(1, brief00.DisputeHistory.Count);
            Assert.AreEqual(null, brief00.ResolutionHistory);

            // seems dao owner is test4
            var dao = (await client.GetLastBlockAsync(trade.Trade.daoId)).As<IDao>();
            Assert.AreEqual(test4PublicKey, dao.OwnerAccountId);

            //// host submit resolution
            var resolution = await CreateODRResolution(trade);
            resolution.CaseId = brief00.DisputeHistory.First().Id;
            var sesret = await dealer.SubmitResolutionAsync(resolution, testPublicKey,
                Signatures.GetSignature(testPrivateKey, lsb.GetBlock().Hash, testPublicKey)
                );
            Assert.IsTrue(sesret.Successful(), $"Should success but {sesret.ResultCode}");

            // submit again will get error
            var sesret2 = await dealer.SubmitResolutionAsync(resolution, testPublicKey,
                Signatures.GetSignature(testPrivateKey, lsb.GetBlock().Hash, testPublicKey)
                );
            Assert.IsTrue(sesret2.ResultCode == APIResultCodes.ResolutionPending, $"should pending but {sesret2.ResultCode}");

            // verify brief
            var brief1 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(DisputeLevels.Peer, brief1.DisputeLevel);

            Assert.AreEqual(1, brief1.DisputeHistory.Count);
            Assert.AreEqual(1, brief1.ResolutionHistory.Count);

            // guest accept the resolution?
            var acpret = await dealer.AnswerToResolutionAsync(trade.AccountID, brief1.ResolutionHistory.First().Id, accepted, test2PublicKey,
                Signatures.GetSignature(test2PrivateKey, lsb.GetBlock().Hash, test2PublicKey)
                );
            Assert.IsTrue(acpret.Successful(), $"Can't answer resolution: {acpret.ResultCode}");

            // verify brief
            var brief2 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            if(accepted)
            {
                Assert.AreEqual(DisputeLevels.None, brief2.DisputeLevel);
            }
            else
            {
                Assert.AreEqual(DisputeLevels.Peer, brief2.DisputeLevel);
            }

            Assert.AreEqual(1, brief2.DisputeHistory.Count);
            Assert.AreEqual(1, brief2.ResolutionHistory.Count);

            if(accepted)
            {
                await GuestCancelTradeAsync(trade);

                await HostCloseOrder(order);
            }
            else
            {
                await CancelTradeShouldFail(trade);

                await CloseOrderShouldFail(order);
            }

            return trade;
        }

        private async Task<IVoting> DaoOwnerCreateAVote(IOtcTrade trade, ODRResolution resolution)
        {
            var title = $"Now let vote on case ID {Random.Shared.NextInt64()}";
            VotingSubject subject = new VotingSubject
            {
                Type = SubjectType.OTCDispute,
                DaoId = trade.Trade.daoId,
                Issuer = test4Wallet.AccountId,
                TimeSpan = 100,
                Title = title,
                Description = "bla bla bla",
                Options = new[] { "Yay", "Nay" },
            };

            var proposal = new VoteProposal
            {
                pptype = ProposalType.DisputeResolution,
                data = JsonConvert.SerializeObject(resolution),
            };

            var voteCrtRet = await test4Wallet.CreateVoteSubject(subject, proposal);
            Assert.IsTrue(voteCrtRet.Successful(), $"Create vote subject error {voteCrtRet.ResultCode}");

            await Task.Delay(4000);
            // find method 2
            var votefindret2 = await test4Wallet.RPC.FindAllVoteForTradeAsync(trade.AccountID);
            Assert.IsTrue(votefindret2.Successful(), $"Can't find vote: {votefindret2.ResultCode}");
            var votes2 = votefindret2.GetBlocks();
            var curvote2 = votes2.Last() as IVoting;
            Assert.AreEqual(subject.Title, curvote2.Subject.Title);

            return curvote2;
        }

        // peer/dealer level
        private async Task GuestComplainLevel0(IOtcTrade trade)
        {
            var lsb = await client.GetLastServiceBlockAsync();
            var brief0 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(trade.AccountID, brief0.TradeId);
            Assert.AreEqual(DisputeLevels.None, brief0.DisputeLevel);
            Assert.AreEqual(null, brief0.DisputeHistory);
            Assert.AreEqual(null, brief0.ResolutionHistory);

            var ret = await dealer.ComplainAsync(trade.AccountID, trade.Trade.collateral, test2PublicKey,
                Signatures.GetSignature(test2PrivateKey, lsb.GetBlock().Hash, test2PublicKey)
                );
            Assert.IsTrue(ret.Successful());
        }

        // dao level
        private async Task GuestComplainLevel1(IOtcTrade trade)
        {
            var lsb = await client.GetLastServiceBlockAsync();
            var brief0 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(trade.AccountID, brief0.TradeId);
            Assert.AreEqual(DisputeLevels.Peer, brief0.DisputeLevel);
            Assert.AreNotEqual(null, brief0.DisputeHistory);
            Assert.AreNotEqual(null, brief0.ResolutionHistory);

            // seller not got the payment. seller raise a dispute
            var crdptret = await test2Wallet.OTCTradeRaiseDisputeAsync(trade.AccountID);
            Assert.IsTrue(crdptret.Successful(), $"Raise dispute failed: {crdptret.ResultCode}");

            await Task.Delay(2000);

            var ret = await dealer.DisputeCreatedAsync(trade.AccountID, test2PublicKey,
                Signatures.GetSignature(test2PrivateKey, lsb.GetBlock().Hash, test2PublicKey));
            Assert.IsTrue(ret.Successful(), $"failed to call DisputeCreatedAsync: {ret.ResultCode}");

            var brief1 = await GetBrief(lsb.GetBlock().Hash, trade.AccountID);
            Assert.AreEqual(trade.AccountID, brief1.TradeId);
            Assert.AreEqual(DisputeLevels.DAO, brief1.DisputeLevel);
            Assert.AreNotEqual(null, brief0.DisputeHistory);
            Assert.AreNotEqual(null, brief0.ResolutionHistory);
        }

        /// <summary>
        /// dispute level: DAO
        /// DAO try to provide resolution and both part will accept it.
        /// </summary>
        /// <returns></returns>
        //[TestMethod]
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
            var resolution = await CreateODRResolution(trade);
            Assert.IsNotNull(resolution);

            // resolution submit to dealer

            // buyer and seller will accept the resolution

            // dao owner execute the resolution
            var ret = await testWallet.ExecuteResolution(null, resolution);
            Assert.IsTrue(ret.Successful(), $"Failed to execute resolution: {ret.ResultCode}");
        }

        private async Task<ODRResolution> CreateODRResolution(IOtcTrade trade)
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
                Creator = testWallet.AccountId,
                TradeId = trade.AccountID,
                Actions = moves,
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
