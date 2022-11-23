using Converto;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.ABI;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UnitTests
{
    [TestClass]
    public class UT_DAO : XTestBase
    {
        IDealer dlr;

        //[TestMethod]
        public async Task TestDAO()
        {
            await SetupWallets("xtest");

            await TestDealerAsync();

            Console.WriteLine("Test Sell Order");
            await TestOTCTradeAsync(TradeDirection.Sell);

            Console.WriteLine("Test Buy Order");
            await TestOTCTradeAsync(TradeDirection.Buy);

            await TestChangeDAO();

            var tradeid = await TestOTCTradeDispute();   // test for dispute
            //await TestVoting(tradeid); // related to dealer. bypass. real test in OTC unit test

            await TestPoolAsync();
            await TestProfitingAndStaking();
            await TestNodeFee();

            // let workflow to finish
            await Task.Delay(1000);            
        }

        private async Task TestChangeDAO()
        {
            // create a DAO for nodes
            var name = "Node Owners Club";
            var desc = "Doing great business!";
            var dcret = await genesisWallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.005m, 10, 120, 120);
            Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

            await WaitWorkflow(dcret.TxHash, "CreateDAOAsync");

            var nodesdaoret = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret.Successful(), $"can't get dao: {nodesdaoret.ResultCode}");
            var nodesdao = nodesdaoret.GetBlock() as TransactionBlock;

            var daoid = nodesdao.AccountID;

            // test dao change
            var change = new DAOChange
            {
                creator = genesisWallet.AccountId,
                settings = new Dictionary<string, string>
                {
                    { "ShareRito", "0.9" },
                    { "Seats", "39" },
                    { "SellerPar", "120" },
                }
            };
            var chgret = await genesisWallet.ChangeDAO(nodesdao.AccountID, null, change);
            Assert.IsTrue(chgret.Successful(), $"Can't change DAO: {chgret.ResultCode}");

            await WaitWorkflow("Change DAO");
            Assert.IsTrue(_authResult);

            // test non-owner
            var chgx21 = await testWallet.ChangeDAO(nodesdao.AccountID, null, change);
            Assert.IsTrue(chgx21.ResultCode == APIResultCodes.Unauthorized, $"Should error change DAO 21: {chgx21.ResultCode}");
            await WaitBlock("Change DAO Wrong 21");

            // wrong creator
            var chgx2 = await genesisWallet.ChangeDAO(nodesdao.AccountID, null, change.With(
                new
                {
                    creator = testWallet.AccountId,
                }
                ));
            Assert.IsTrue(chgx2.ResultCode == APIResultCodes.Unauthorized, $"Should error change DAO 2: {chgx2.ResultCode}");
            await WaitBlock("Change DAO Wrong 2");
            // wrong desc
            var chgx22 = await genesisWallet.ChangeDAO(nodesdao.AccountID, null, change.With(
                new
                {
                    settings = new Dictionary<string, string>
                    {
                        {"Description", null }
                    }
                }
                ));
            Assert.IsTrue(chgx22.ResultCode == APIResultCodes.ArgumentOutOfRange, $"Should error change DAO 22: {chgx22.ResultCode}");
            await WaitBlock("Change DAO Wrong 22");

            // wrong settings
            var chgx23 = await genesisWallet.ChangeDAO(nodesdao.AccountID, null, change.With(
                new
                {
                    settings = new Dictionary<string, string>
                    {
                        {"aaaa", null }
                    }
                }
                ));
            Assert.IsTrue(chgx23.ResultCode == APIResultCodes.InvalidArgument, $"Should error change DAO 23: {chgx23.ResultCode}");
            await WaitBlock("Change DAO Wrong 23");

            // test out of range settings
            change.settings["ShareRito"] = "1.2";
            change.settings["Description"] = null;
            var chgx1 = await genesisWallet.ChangeDAO(nodesdao.AccountID, null, change);
            Assert.IsTrue(!chgx1.Successful(), $"Should error change DAO: {chgx1.ResultCode}");
            await WaitBlock("Change DAO Wrong 1");




            await TestJoinDAO(daoid);

            // test dao change by vote
            VotingSubject daochg = new VotingSubject
            {
                Type = SubjectType.DAOModify,
                DaoId = nodesdao.AccountID,
                Issuer = genesisWallet.AccountId,
                TimeSpan = 100,
                Title = "We need to modify DAO",
                Description = "Change these settings",
                Options = new[] { "Yay", "Nay" },
            };

            var change2 = new DAOChange
            {
                creator = genesisWallet.AccountId,
                settings = new Dictionary<string, string>
                {
                    { "ShareRito", "0.7" },
                    { "Seats", "30" },
                    { "SellerPar", "130" },
                    { "BuyerPar", "170" },
                    { "Description", "new desc" },
                }
            };

            var daoprosl = new VoteProposal
            {
                pptype = ProposalType.DAOSettingChanges,
                data = JsonConvert.SerializeObject(change2),
            };

            var daoVoteCrtRet = await genesisWallet.CreateVoteSubject(daochg, daoprosl);
            await WaitWorkflow("Create Vote for dao change Async");
            Assert.IsTrue(daoVoteCrtRet.Successful(), $"Create vote for dao error: {daoVoteCrtRet.ResultCode}");

            await DoVote(daoVoteCrtRet.TxHash, true);

            var voteblksRet = await genesisWallet.RPC.GetBlocksByRelatedTxAsync(daoVoteCrtRet.TxHash);
            var blockdvret = await genesisWallet.RPC.GetLastBlockAsync((voteblksRet.GetBlocks().Last() as TransactionBlock).AccountID);
            Assert.IsTrue(blockdvret.Successful(), $"Can't get vote {blockdvret.ResultCode}");
            var blockdv = blockdvret.GetBlock() as TransactionBlock;

            var summaryxret = await test4Wallet.RPC.GetVoteSummaryAsync(blockdv.AccountID);
            Assert.IsTrue(summaryxret.Successful(), $"failed to get vote summary: {summaryxret.ResultCode}, {summaryxret.ResultMessage}");
            var summaryx = JsonConvert.DeserializeObject<VotingSummary>(summaryxret.JsonString);
            Assert.IsNotNull(summaryx, "can't get vote summary.");
            //Assert.IsFalse(summaryx.IsDecided, "should not be decided.");

            var chgret2 = await genesisWallet.ChangeDAO(nodesdao.AccountID, blockdv.AccountID, change2);
            Assert.IsTrue(chgret2.Successful(), $"Can't change DAO: {chgret2.ResultCode}");
            await WaitWorkflow("Change DAO 2 by vote");
            Assert.IsTrue(_authResult);

            // test api
            var execret = await genesisWallet.RPC.FindExecForVoteAsync(blockdv.AccountID);
            Assert.IsTrue(execret.Successful());
            Assert.AreEqual(BlockTypes.OrgnizationChange, execret.GetBlock().BlockType);

            // test if dup exec detected
            var chgret3 = await genesisWallet.ChangeDAO(nodesdao.AccountID, blockdv.AccountID, change2);
            Assert.IsTrue(chgret3.ResultCode == APIResultCodes.AlreadyExecuted, $"Can't change DAO: {chgret3.ResultCode}");
            await WaitBlock("Change DAO 3 by vote");

            // inconsist changes
            var chgret31 = await genesisWallet.ChangeDAO(nodesdao.AccountID, blockdv.AccountID, change2
                .With(
                    new
                    {
                        settings = new Dictionary<string, string>()
                    }
                ));
            Assert.IsTrue(chgret31.ResultCode == APIResultCodes.ArgumentOutOfRange, $"Can't change DAO 31: {chgret31.ResultCode}");
            await WaitBlock("Change DAO 31 by vote");
        }

        private async Task TestJoinDAO(string daoid)
        {
            // get dao
            var nodesdaoret = await genesisWallet.RPC.GetLastBlockAsync(daoid);
            Assert.IsTrue(nodesdaoret.Successful(), $"can't get dao: {nodesdaoret.ResultCode}");
            var nodesdao = nodesdaoret.GetBlock() as TransactionBlock;
            var name = (nodesdao as IDao).Name;

            // join DAO / invest
            var invret0 = await testWallet.JoinDAOAsync(daoid, 800m);
            Assert.IsTrue(invret0.ResultCode == APIResultCodes.InvalidAmount);
            await WaitBlock("JoinDAOAsync 0");

            var invret = await testWallet.JoinDAOAsync(daoid, 800000m);
            Assert.IsTrue(invret.Successful());
            await WaitWorkflow("JoinDAOAsync 1");

            nodesdaoret = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret.Successful());
            nodesdao = nodesdaoret.GetBlock() as TransactionBlock;
            var treasure = (nodesdao as IDao).Treasure.ToDecimalDict();
            Assert.AreEqual(800000m, Math.Round(treasure[testPublicKey], 5));

            // another join DAO
            var invret2 = await test2Wallet.JoinDAOAsync(daoid, 150000m);
            Assert.IsTrue(invret2.Successful());

            await WaitWorkflow("JoinDAOAsync 2");

            var invret3 = await test3Wallet.JoinDAOAsync(daoid, 50000m);
            Assert.IsTrue(invret3.Successful());

            await WaitWorkflow("JoinDAOAsync 3");

            var invret4 = await test4Wallet.JoinDAOAsync(daoid, 50000m);
            Assert.IsTrue(invret4.Successful());

            await WaitWorkflow("JoinDAOAsync 4");

            // then we expect the treasure rito
            nodesdaoret = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret.Successful());
            nodesdao = nodesdaoret.GetBlock() as TransactionBlock;
            treasure = (nodesdao as IDao).Treasure.ToDecimalDict();
            Assert.AreEqual(800000m, Math.Round(treasure[testPublicKey], 5));
            Assert.AreEqual(150000m, Math.Round(treasure[test2PublicKey], 5));
            Assert.AreEqual(50000m, Math.Round(treasure[test3PublicKey], 5));
            Assert.AreEqual(50000m, Math.Round(treasure[test4PublicKey], 5));

            // test leave DAO
            var leaveret4 = await test4Wallet.LeaveDAOAsync(daoid);
            Assert.IsTrue(leaveret4.Successful(), $"Can't leave DAO: {leaveret4.ResultCode}");
            await WaitWorkflow("LeaveDAOAsync 4");

            // then test3 should not exists in the treasure
            var nodesdaoret2 = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret2.Successful());
            var nodesdao2 = nodesdaoret2.GetBlock() as TransactionBlock;
            var treasure2 = (nodesdao2 as IDao).Treasure.ToDecimalDict();
            Assert.IsFalse(treasure2.ContainsKey(test4PublicKey), $"test 4 still exists.");
        }

        private async Task TestVoting(string disputeTradeId)
        {
            // get the dispute trade
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(disputeTradeId);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.Dispute, (trdlatest.GetBlock() as IOtcTrade).OTStatus,
                $"Trade statust is not dispute");
            var trade = trdlatest.GetBlock() as IOtcTrade;
            var daoid = trade.Trade.daoId;

            var daolatestret = await test2Wallet.RPC.GetLastBlockAsync(daoid);
            Assert.IsTrue(daolatestret.Successful());
            var daolatest = daolatestret.GetBlock() as IDao;
            var name = daolatest.Name;

            await TestJoinDAO((daolatest as TransactionBlock).AccountID);

            // dispute trade
            // seller: testwallet
            // buyer: test2wallet
            // dispute created by: testwallet

            VotingSubject subject = new VotingSubject
            {
                Type = SubjectType.OTCDispute,
                DaoId = trade.Trade.daoId,
                Issuer = testWallet.AccountId,
                TimeSpan = 100,
                Title = "Now let vote on case ID 111",
                Description = "bla bla bla",
                Options = new [] { "Yay", "Nay"},
            };

            var resolution = new ODRResolution
            {
                RType = ResolutionType.OTCTrade,
                Creator = testWallet.AccountId,
                TradeId = disputeTradeId,
                Actions = new []
                {
                    new TransMove
                    {
                        from = Parties.DAOTreasure,
                        to = Parties.Buyer,
                        amount = 100,
                        desc = "compensate"
                    },
                    new TransMove
                    {
                        from = Parties.DAOTreasure,
                        to = Parties.Seller,
                        amount = 100,
                        desc = "compensate"
                    }
                }
            };

            var proposal = new VoteProposal
            {
                pptype = ProposalType.DisputeResolution,
                data = JsonConvert.SerializeObject(resolution),
            };

            var voteCrtRet = await genesisWallet.CreateVoteSubject(subject, proposal);

            await WaitWorkflow("Create Vote Subject Async");
            Assert.IsTrue(voteCrtRet.Successful(), $"Create vote subject error {voteCrtRet.ResultCode}");

            // then we will find the vote
            var votefindret = await genesisWallet.RPC.FindAllVotesByDaoAsync(trade.Trade.daoId, true);
            Assert.IsTrue(votefindret.Successful(), $"Can't find vote: {votefindret.ResultCode}");
            var votes = votefindret.GetBlocks();
            Assert.AreEqual(1, votes.Count());
            var curvote = votes.Last() as IVoting;
            Assert.AreEqual(subject.Title, curvote.Subject.Title);

            // find method 2
            var votefindret2 = await genesisWallet.RPC.FindAllVoteForTradeAsync(disputeTradeId);
            Assert.IsTrue(votefindret2.Successful(), $"Can't find vote: {votefindret2.ResultCode}");
            var votes2 = votefindret2.GetBlocks();
            Assert.AreEqual(1, votes2.Count());
            var curvote2 = votes2.Last() as IVoting;
            Assert.AreEqual(subject.Title, curvote2.Subject.Title);

            // call vote
            await DoVote(voteCrtRet.TxHash, true);

            var voteRet4 = await test4Wallet.Vote((curvote as TransactionBlock).AccountID, 1);
            Assert.IsTrue(voteRet4.ResultCode == APIResultCodes.Unauthorized, $"Vote 4 should error: {voteRet4.ResultCode}");
            await WaitBlock("Vote on Subject Async 4");

            // join after vote genesis should also error
            var invret4 = await test4Wallet.JoinDAOAsync(trade.Trade.daoId, 50000m);
            Assert.IsTrue(invret4.Successful());
            await WaitWorkflow("join after vote genesis");

            var voteRet41 = await test4Wallet.Vote((curvote as TransactionBlock).AccountID, 1);
            Assert.IsTrue(voteRet41.ResultCode == APIResultCodes.Unauthorized, $"Vote 41 should error: {voteRet41.ResultCode}");
            await WaitBlock("Vote on Subject Async 41");

            // clean
            var leaveret4 = await test4Wallet.LeaveDAOAsync(trade.Trade.daoId);
            Assert.IsTrue(leaveret4.Successful(), $"Can't leave DAO: {leaveret4.ResultCode}");
            await WaitWorkflow("clean join after vote genesis");

            // owner create resolution on vote result
            // vote keep as is.
            var summaryret = await test4Wallet.RPC.GetVoteSummaryAsync((curvote as TransactionBlock).AccountID);
            Assert.IsTrue(summaryret.Successful());
            var summary = JsonConvert.DeserializeObject<VotingSummary>(summaryret.JsonString);

            Assert.IsNotNull(summary, "can't get vote summary.");
            Assert.IsTrue(summary.IsDecided, "should be decided.");
            Assert.AreEqual(0, summary.DecidedIndex, $"voting decided wrong option: {summary.DecidedIndex}");

            // trade should be dispute state
            var res1 = summary.Spec.Proposal.Deserialize() as ODRResolution;
            var latestTradeRet = await genesisWallet.RPC.GetLastBlockAsync(res1.TradeId);
            var latestTrade = latestTradeRet.GetBlock() as IOtcTrade;
            Assert.AreEqual(OTCTradeStatus.Dispute, latestTrade.OTStatus);

            await test2Wallet.SyncAsync(null);
            var beforeresolv = test2Wallet.BaseBalance;

            // TODO: upgrade according to the latest ODR design
            return;

            // then we execute the resolution depend on the voting result
            var odrRet = await genesisWallet.ExecuteResolution(summary.Spec.AccountID, res1);
            Assert.IsTrue(odrRet.Successful(), $"can't execute resolution: {odrRet.ResultCode}");

            await WaitWorkflow("ExecuteResolution");

            // now the state should be DisputeClosed 
            latestTradeRet = await genesisWallet.RPC.GetLastBlockAsync(res1.TradeId);
            latestTrade = latestTradeRet.GetBlock() as IOtcTrade;
            Assert.AreEqual(OTCTradeStatus.DisputeClosed, latestTrade.OTStatus);

            // testwallet should receive the compensate
            await test2Wallet.SyncAsync(null);
            var afterresolv = test2Wallet.BaseBalance;
            Assert.AreEqual(beforeresolv + 100m, afterresolv, $"compensate not received.");

            ResetAuthFail();
        }

        private async Task DoVote(string votehash, bool success)
        {
            Console.WriteLine($"Vote on {votehash} as {success}");
            var voteblksRet = await genesisWallet.RPC.GetBlocksByRelatedTxAsync(votehash);
            var voteblk = voteblksRet.GetBlocks().Last() as TransactionBlock;
            var voteRet = await testWallet.Vote(voteblk.AccountID, 0);
            Assert.IsTrue(voteRet.Successful(), $"Vote error: {voteRet.ResultCode}");
            await WaitWorkflow("Vote on Subject Async");

            var voteRet2 = await test2Wallet.Vote(voteblk.AccountID, 1);
            Assert.IsTrue(voteRet2.Successful(), $"Vote error: {voteRet2.ResultCode}");
            await WaitWorkflow("Vote on Subject Async 2");

            var voteRet2x = await test2Wallet.Vote(voteblk.AccountID, 0);
            Assert.IsTrue(!voteRet2x.Successful(), $"Vote 2x should error: {voteRet2x.ResultCode}");
            await WaitBlock("Vote on Subject Async 2x");

            ResetAuthFail();

            if (success)
            {
                var voteRet3 = await test3Wallet.Vote(voteblk.AccountID, 0);
                await WaitWorkflow("Vote on Subject Async 3");
                Assert.IsTrue(voteRet3.Successful(), $"Vote error: {voteRet3.ResultCode}");
            }            
        }

        private async Task TestOTCTradeAsync(TradeDirection direction)
        {
            var crypto = "tether/ETH";
            bool firstTime = false;

            await testWallet.SyncAsync(null);
            if (!testWallet.GetLastSyncBlock().Balances.ContainsKey(crypto))
            {
                // init. create token to sell
                var tokenGenesisResult = await testWallet.CreateTokenAsync("ETH", "tether", "", 8, 100000, false, testWallet.AccountId,
                        "", "", ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(tokenGenesisResult.Successful(), $"test otc token genesis failed: {tokenGenesisResult.ResultCode} for {testWallet.AccountId}");

                await WaitBlock("CreateTokenAsync");

                await testWallet.SyncAsync(null);

                await testWallet.SendAsync(100, test2PublicKey, crypto);
                await test2Wallet.SyncAsync(null);

                firstTime = true;
            }

            var testbalance = testWallet.BaseBalance;

            // first create a DAO
            var name = "First DAO";
            var desc = "Doing great business!";

            if(firstTime)
            {
                var dcret = await testWallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.001m, 10, 120, 130);
                Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

                await WaitWorkflow("CreateDAOAsync");
            }

            var daoret = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret.Successful(), $"Can't get DAO: {daoret.ResultCode}");
            var daoblk = daoret.GetBlock() as IDao;
            Assert.AreEqual(name, daoblk.Name);
            Assert.AreEqual(desc, daoblk.Description);
            Assert.AreEqual(1, daoblk.ShareRito);
            Assert.AreEqual(0.01m, daoblk.SellerFeeRatio);
            Assert.AreEqual(0.001m, daoblk.BuyerFeeRatio);
            Assert.AreEqual(120, daoblk.SellerPar);
            Assert.AreEqual(130, daoblk.BuyerPar);

            //var dcretx = await testWallet.CreateDAOAsync(name, desc);
            //Assert.IsTrue(!dcretx.Successful(), $"should failed to create DAO: {dcretx.ResultCode}");

            //await WaitBlock("CreateDAOAsync Wrong");
            //ResetAuthFail();

            // test getalldao api
            var alldaoret = await testWallet.RPC.GetAllDaosAsync(0, 10);
            Assert.IsTrue(alldaoret.Successful(), $"can get all dao: {alldaoret.ResultCode}");
            var daos = alldaoret.GetBlocks();
            Assert.AreEqual(1, daos.Count(), $"can't find dao by GetAllDaosAsync");
            var dao0 = alldaoret.GetBlocks().First() as DaoGenesisBlock;
            //Assert.IsTrue(daoblk.AuthCompare(dao0));

            // get dao by the IBroker api
            var brkblksret = await testWallet.RPC.GetAllBrokerAccountsForOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(brkblksret.Successful(), $"Can't get DAO by brk api: {brkblksret.ResultCode}");
            var daoblk2 = brkblksret.GetBlocks().FirstOrDefault(a => a is DaoGenesisBlock) as DaoGenesisBlock;
            Assert.AreEqual(name, daoblk2.Name);
            Assert.AreEqual(desc, daoblk2.Description);

            var dao1 = daoret.GetBlock() as IDao;

            var prices = await dealer.GetPricesAsync();
            var order = new OTCOrder
            {
                daoId = dao1.AccountID,
                dealerId = dlr.AccountID,
                dir = direction,
                crypto = crypto,
                fiat = fiat,
                fiatPrice = prices[fiat.ToLower()],
                priceType = PriceType.Fixed,
                price = 2000,
                collateral = 75_000_000,
                collateralPrice = prices["LYR"],
                payBy = new string[] { "Paypal" },

                amount = 1,
                limitMin = 200,
                limitMax = 2000,
            };

            var ret = await testWallet.CreateOTCOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"Can't create order: {ret.ResultCode}");

            await WaitWorkflow($"CreateOTCOrderAsync {direction}");

            var otcret = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret.Successful(), $"Can't get otc gensis block. {otcret.ResultCode}");
            var otcs = otcret.GetBlocks();
            Assert.IsTrue(otcs.First() is IOtcOrder, $"otc order gensis block not found.");

            await CheckDAO(name, desc);

            // test find tradable orders
            var tradableret = await testWallet.RPC.FindTradableOtcAsync();
            Assert.IsTrue(tradableret.Successful(), "Unable to find tradable.");
            var tradableblks = tradableret.GetBlocks("orders");
            Assert.AreEqual(1, tradableblks.Count(), $"Trade {direction} tradable block count is {tradableblks.Count()}");
            var firsttradable = tradableblks.First();
            Assert.IsTrue(firsttradable is IOtcOrder fodr && fodr.Name == "no name");

            // then DAO treasure should not have the crypto
            var daoret3 = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret3.Successful(), $"Can't get DAO: {daoret3.ResultCode}");
            var daot = daoret3.GetBlock() as TransactionBlock;
            Assert.IsTrue(daot.Balances.ContainsKey(crypto), "No collateral token in DAO treasure.");
            Assert.AreEqual(0, daot.Balances[crypto].ToBalanceDecimal());

            var otcg = otcs.Last() as OTCOrderGenesisBlock;
            Assert.IsTrue(order.Equals(otcg.Order), "OTC order not equal.");

            await test2Wallet.SyncAsync(null);
            var test2balance = test2Wallet.BaseBalance;

            var tradgen = await CreateOTCTradeAsync(dao1 as TransactionBlock, otcg, direction == TradeDirection.Sell ? TradeDirection.Buy : TradeDirection.Sell);
            await CancelOTCTrade(dao1 as TransactionBlock, tradgen);
            await test2Wallet.SyncAsync(null);
            var test2balanceA = test2Wallet.BaseBalance;
            Assert.AreEqual(test2balance - 13m, test2balanceA, "Balance not ok after cancel trade.");

            tradgen = await CreateOTCTradeAsync(dao1 as TransactionBlock, otcg, direction == TradeDirection.Sell ? TradeDirection.Buy : TradeDirection.Sell);
            // cancel one

            await CheckDAO(name, desc);

            // buyer send payment indicator
            var wlt = direction == TradeDirection.Sell ? test2Wallet : testWallet;
            AuthorizationAPIResult payindret = await wlt.OTCTradeFiatPaymentSentAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

            await WaitWorkflow($"OTCTradeBuyerPaymentSentAsync {direction}");
            // status changed to BuyerPaid
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.FiatSent, (trdlatest.GetBlock() as IOtcTrade).OTStatus,
                $"Trade statust not changed to BuyerPaid");

            // seller got the payment
            var wlt2 = direction == TradeDirection.Sell ? testWallet : test2Wallet;
            var gotpayret = await wlt2.OTCTradeFiatPaymentConfirmAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Got Payment indicator error: {payindret.ResultCode}");

            await WaitWorkflow($"OTCTradeSellerGotPaymentAsync {direction}");

            // status changed to BuyerPaid
            var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.CryptoReleased, (trdlatest2.GetBlock() as IOtcTrade).OTStatus,
                $"Trade status not changed to ProductReleased");

            await test2Wallet.SyncAsync(null);

            // buyer fee calculated as LYR
            var totalAmount = tradgen.Trade.amount;
            decimal totalFee = 0;
            var trade = tradgen.Trade;
            // transaction fee
            if (trade.dir == TradeDirection.Sell)
            {
                totalFee += Math.Round((((totalAmount * trade.price) * order.fiatPrice) * daoblk.SellerFeeRatio) / order.collateralPrice, 8);
            }
            else
            {
                totalFee += Math.Round((((totalAmount * trade.price) * order.fiatPrice) * daoblk.BuyerFeeRatio) / order.collateralPrice, 8);
            }

            // network fee
            var networkFee = Math.Round((((totalAmount * order.price) * order.fiatPrice) * 0.002m) / order.collateralPrice, 8);

            var buyerfee = totalFee + networkFee;
            Console.WriteLine($"Cost calculated txfee: {totalFee} netfee: {networkFee} Real: {test2balance - test2Wallet.BaseBalance} should be: {totalFee + networkFee + 26}");
            var buyershouldget = test2balance - 13 - buyerfee - 13;
            // create trade 10 lyr, send confirm 1, fee 2, cancel 13
            Assert.AreEqual(buyershouldget, test2Wallet.BaseBalance, $"Test2 got collateral wrong. should be {buyershouldget} but {test2Wallet.BaseBalance} diff {buyershouldget - test2Wallet.BaseBalance}");

            // delist the order
            Console.WriteLine($"Delisting order: {otcg.AccountID}");
            var dlret = await testWallet.DelistOTCOrderAsync(dao1.AccountID, otcg.AccountID);
            Assert.IsTrue(dlret.Successful(), $"Unable to delist order: {dlret.ResultCode}");
            await WaitWorkflow($"DelistOTCOrderAsync {direction}");

            await CheckDAO(name, desc);

            var orddlret = await testWallet.RPC.GetLastBlockAsync(otcg.AccountID);
            Assert.IsTrue(orddlret.Successful(), $"Can't get order latest block: {orddlret.ResultCode}");
            Assert.AreEqual(OTCOrderStatus.Delist, (orddlret.GetBlock() as IOtcOrder).OOStatus,
                $"Order status not changed to Delisted");

            // trade is ok. now its time to close the order
            var closeret = await testWallet.CloseOTCOrderAsync(dao1.AccountID, otcg.AccountID);
            Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            await WaitWorkflow($"CloseOTCOrderAsync {direction}");
            var ordfnlret = await testWallet.RPC.GetLastBlockAsync(otcg.AccountID);
            Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
            Assert.AreEqual(OTCOrderStatus.Closed, (ordfnlret.GetBlock() as IOtcOrder).OOStatus,
                $"Order status not changed to Closed: {(ordfnlret.GetBlock() as IOtcOrder).OOStatus}");
            Assert.AreEqual(0, (ordfnlret.GetBlock() as TransactionBlock).Balances["LYR"], "LYR not zero");

            await CheckDAO(name, desc);

            await testWallet.SyncAsync(null);

            if (order.dir == TradeDirection.Sell)
            {
                totalFee = Math.Round((((totalAmount * order.price) * order.fiatPrice) * daoblk.SellerFeeRatio) / order.collateralPrice, 8);
            }
            else
            {
                totalFee = Math.Round((((totalAmount * order.price) * order.fiatPrice) * daoblk.BuyerFeeRatio) / order.collateralPrice, 8);
            }

            var networkfeeToPay = Math.Round((((2000m * 0.1m) * order.fiatPrice) * 0.002m) / order.collateralPrice, 8);
            var lyrshouldbe = testbalance - 10000 - 10 - 4 - 1 - totalFee - networkfeeToPay; 
            // mint, create order, 4 send, 1 LYR for close order
            
            if (!firstTime)
                lyrshouldbe += 10000;

            Assert.AreEqual(lyrshouldbe, testWallet.BaseBalance, $"Test got collateral wrong. should be {lyrshouldbe} but {testWallet.BaseBalance} diff {lyrshouldbe - testWallet.BaseBalance}");
            var bal2 = testWallet.GetLastSyncBlock().Balances[crypto].ToBalanceDecimal();

            decimal x = firstTime ? 0.1m : 0;
            Assert.AreEqual(100000m - x - 100, bal2,
                $"Trade after {direction} testwallet balance of crypto should be {100010m - x - 100} but {bal2}");

            // dao should be kept
            await CheckDAO(name, desc);

            await Task.Delay(100);
            Assert.IsTrue(_authResult, $"Authorizer failed: {_sbAuthResults}");
            ResetAuthFail();
        }

        private async Task CheckDAO(string name, string desc)
        {
            var daoretv = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoretv.Successful(), $"Can't get DAO: {daoretv.ResultCode}");
            var daoblkv = daoretv.GetBlock() as IDao;
            Assert.AreEqual(name, daoblkv.Name);
            Assert.AreEqual(desc, daoblkv.Description);
            Assert.AreEqual(1, daoblkv.ShareRito);
            Assert.AreEqual(0.01m, daoblkv.SellerFeeRatio);
            Assert.AreEqual(0.001m, daoblkv.BuyerFeeRatio);
            Assert.AreEqual(120, daoblkv.SellerPar);
            Assert.AreEqual(130, daoblkv.BuyerPar);
        }

        private async Task CancelOTCTrade(TransactionBlock dao, OtcTradeGenesisBlock tradgen)
        {
            // make sure the status of trade is Open
            Assert.AreEqual(OTCTradeStatus.Open, tradgen.OTStatus, "Wrong trade status");
            
            var cloret = await test2Wallet.CancelOTCTradeAsync(tradgen.Trade.daoId, tradgen.Trade.orderId, tradgen.AccountID);
            // check locked IDs
            await WaitBlock("CancelOTCTradeAsync");
            Assert.IsTrue(cloret.Successful());

            Assert.AreEqual(4, _lastAuthResult.LockedIDs.Count, "ID not locked properly");
            Assert.IsTrue(_lastAuthResult.LockedIDs.Contains(tradgen.Trade.daoId));
            Assert.IsTrue(_lastAuthResult.LockedIDs.Contains(tradgen.Trade.orderId));
            Assert.IsTrue(_lastAuthResult.LockedIDs.Contains(tradgen.AccountID));

            // try lock it
            var cloret2 = await test2Wallet.CancelOTCTradeAsync(tradgen.Trade.daoId, tradgen.Trade.orderId, tradgen.AccountID);
            //await WaitBlock("CancelOTCTradeAsync 2");
            Assert.AreEqual(APIResultCodes.ResourceIsBusy, cloret2.ResultCode, $"Not locked properly: {cloret2.ResultCode}");

            await WaitBlock("CancelOTCTradeAsync");
            //await WaitWorkflow("CancelOTCTradeAsync", false);

            ResetAuthFail();

            Assert.IsTrue(cloret.Successful(), $"Unable to cancel trade: {cloret.ResultCode}");

            // make sure the status of trade is Closed
            var latestret = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(latestret.Successful());
            var tradelst = latestret.GetBlock() as IOtcTrade;
            Assert.AreEqual(OTCTradeStatus.Canceled, tradelst.OTStatus, "not close trade properly");
        }

        private async Task<OtcTradeGenesisBlock> CreateOTCTradeAsync(TransactionBlock dao1, OTCOrderGenesisBlock otcg, TradeDirection direction)
        {
            // here comes a buyer, he who want to buy 1 BTC.
            var tradableret = await testWallet.RPC.FindTradableOtcAsync();
            Assert.IsTrue(tradableret.Successful(), $"Can't find tradableorders: {tradableret.ResultCode}: {tradableret.ResultMessage}");
            var ords = tradableret.GetBlocks("orders");
            Assert.AreEqual(1, ords.Count(), "Order count not right");
            //Assert.IsTrue((ords.First() as IOtcOrder).Order.Equals(order), "OTC order not equal.");

            var trade = new OTCTrade
            {
                daoId = dao1.AccountID,
                dealerId = otcg.Order.dealerId,
                orderId = otcg.AccountID,
                orderOwnerId = otcg.OwnerAccountId,
                dir = direction,
                crypto = "tether/ETH",
                fiat = fiat,
                price = 2000,
                
                collateral = 150000000,
                payVia = "Paypal",
                amount = 0.1m,
                pay = 200,
            };

            var traderet = await test2Wallet.CreateOTCTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"OTC Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow($"CreateOTCTradeAsync for {direction}");
            // the otc order should now be amount 9
            var otcret2 = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret2.Successful(), $"Can't get otc block. {otcret2.ResultCode}");
            var otcs2 = otcret2.GetBlocks();
            Assert.IsTrue(otcs2.Last() is IOtcOrder, $"otc block count not = 1.");
            var otcorderx = otcs2.Last() as IOtcOrder;

            //if(direction == TradeDirection.Buy)
            //    Assert.IsTrue(0.9m == otcorderx.Order.amount, "order not processed");
            //Assert.AreEqual(0.9m, otcorderx.Order.amount, "order not processed");

            // get trade
            var related = await test2Wallet.RPC.GetBlocksByRelatedTxAsync(traderet.TxHash);
            Assert.IsTrue(related.Successful(), $"Can't get rleated tx for trade genesis: {related.ResultCode}");
            var blks = related.GetBlocks();
            var tradgen = blks.LastOrDefault(a => a is OtcTradeGenesisBlock) as OtcTradeGenesisBlock;
            Assert.IsNotNull(tradgen, $"Can't get trade genesis: blks count: {blks.Count()}");
            Assert.AreEqual(trade, tradgen.Trade);
            Assert.AreEqual(OTCTradeStatus.Open, tradgen.OTStatus);

            // verify by api
            var tradeQueryRet = await test2Wallet.RPC.FindOtcTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks();
            Assert.IsTrue(tradeQueryResultBlocks.Count() >= 1);
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks
                .OrderBy(a => a.TimeStamp)
                .Last() as TransactionBlock).AccountID);

            var tradeQueryRet2 = await testWallet.RPC.FindOtcTradeAsync(testWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet2.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet2.ResultCode}");
            var tradeQueryResultBlocks2 = tradeQueryRet2.GetBlocks();
            //Assert.AreEqual(1, tradeQueryResultBlocks2.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks2
                .OrderBy(a => a.TimeStamp)
                .Last() as TransactionBlock).AccountID);

            var tradeQueryRet3 = await testWallet.RPC.FindOtcTradeByStatusAsync(dao1.AccountID, OTCTradeStatus.Open, 0, 10);
            Assert.IsTrue(tradeQueryRet3.Successful(), $"Can't query trade via FindOtcTradeByStatusAsync: {tradeQueryRet3.ResultCode}");
            var tradeQueryResultBlocks3 = tradeQueryRet3.GetBlocks();
            //Assert.AreEqual(1, tradeQueryResultBlocks3.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks3.Last() as TransactionBlock).AccountID);

            return tradgen;
        }

        private async Task<string> TestOTCTradeDispute()
        {
            var crypto = "tether/ETH";
            // init. create token to sell
            //var tokenGenesisResult = await testWallet.CreateTokenAsync("ETH", "unittest", "", 8, 100000, false, testWallet.AccountId,
            //        "", "", ContractTypes.Cryptocurrency, null);
            //Assert.IsTrue(tokenGenesisResult.Successful(), $"test otc token genesis failed: {tokenGenesisResult.ResultCode}");

            //await WaitBlock("CreateTokenAsync");

            await testWallet.SyncAsync(null);
            var testbalance = testWallet.BaseBalance;

            // first create a DAO
            var name = "Second DAO";
            var desc = "Doing bad business!";
            var dcret = await testWallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.01m, 10, 120, 120);
            Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

            await WaitWorkflow("CreateDAOAsync");

            var daoret = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret.Successful(), $"Can't get DAO: {daoret.ResultCode}");
            var daoblk = daoret.GetBlock() as DaoGenesisBlock;
            Assert.AreEqual(name, daoblk.Name);
            Assert.AreEqual(desc, daoblk.Description);

            var dcretx = await testWallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.01m, 10, 120, 120);
            Assert.IsTrue(!dcretx.Successful(), $"should failed to create DAO: {dcretx.ResultCode}");

            await WaitBlock("CreateDAOAsync Wrong");
            ResetAuthFail();

            // get dao by the IBroker api
            var brkblksret = await testWallet.RPC.GetAllBrokerAccountsForOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(brkblksret.Successful(), $"Can't get DAO by brk api: {brkblksret.ResultCode}");
            var daoblk2 = brkblksret.GetBlocks().Skip(1).FirstOrDefault(a => a is DaoGenesisBlock) as DaoGenesisBlock;
            Assert.AreEqual(name, daoblk2.Name);
            Assert.AreEqual(desc, daoblk2.Description);

            var dao1 = daoret.GetBlock() as DaoRecvBlock;

            var prices = await dealer.GetPricesAsync();
            var order = new OTCOrder
            {
                daoId = dao1.AccountID,
                dealerId = dlr.AccountID,
                dir = TradeDirection.Sell,
                crypto = crypto,
                fiat = fiat,
                fiatPrice = prices[fiat.ToLower()],
                priceType = PriceType.Fixed,
                price = 2000,
                amount = 2,
                collateral = 180000000,
                collateralPrice = prices["LYR"],
                payBy = new string[] { "Paypal" },
                limitMin = 200,
                limitMax = 1000,
            };

            var ret = await testWallet.CreateOTCOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"Can't create order: {ret.ResultCode}");

            await WaitWorkflow($"CreateOTCOrderAsync dispute sell");

            var otcret = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret.Successful(), $"Can't get otc gensis block. {otcret.ResultCode}");
            var otcs = otcret.GetBlocks();
            Assert.IsTrue(otcs.Last() is OTCOrderGenesisBlock, $"otc order gensis block not found.");

            // then DAO treasure should not have the crypto
            var daoret3 = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret3.Successful(), $"Can't get DAO: {daoret3.ResultCode}");
            var daot = daoret3.GetBlock() as TransactionBlock;
            Assert.IsTrue(daot.Balances.ContainsKey(crypto), "No collateral token in DAO treasure.");
            Assert.AreEqual(0, daot.Balances[crypto].ToBalanceDecimal());

            var otcg = otcs.Last() as OTCOrderGenesisBlock;
            Assert.IsTrue(order.Equals(otcg.Order), "OTC order not equal.");

            // here comes a buyer, he who want to buy 1 BTC.
            var tradableret = await testWallet.RPC.FindTradableOtcAsync();
            Assert.IsTrue(tradableret.Successful(), $"Can't find tradableorders: {tradableret.ResultCode}: {tradableret.ResultMessage}");
            var ords = tradableret.GetBlocks("orders");
            Assert.AreEqual(1, ords.Count(), "Order count not right");
            Assert.IsTrue((ords.First() as IOtcOrder).Order.Equals(order), "OTC order not equal.");

            var trade = new OTCTrade
            {
                daoId = dao1.AccountID,
                dealerId = otcg.Order.dealerId,
                orderId = otcg.AccountID,
                orderOwnerId = otcg.OwnerAccountId,
                dir = TradeDirection.Buy,
                crypto = "tether/ETH",
                fiat = fiat,
                price = 2000,
                amount = 0.1m,
                collateral = 40000000,
                pay = 200,
                payVia = "Paypal",
            };
            await test2Wallet.SyncAsync(null);
            var test2balance = test2Wallet.BaseBalance;
            var traderet = await test2Wallet.CreateOTCTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"OTC Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow("CreateOTCTradeAsync");
            // the otc order should now be amount 9
            var otcret2 = await testWallet.RPC.GetOtcOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(otcret2.Successful(), $"Can't get otc block. {otcret2.ResultCode}");
            var otcs2 = otcret2.GetBlocks();
            Assert.IsTrue(otcs2.Last() is IOtcOrder, $"otc block count not = 1.");
            var otcorderx = otcs2.Last() as IOtcOrder;
            Assert.AreEqual(1.9m, otcorderx.Order.amount, "order not processed");

            // get trade
            var related = await test2Wallet.RPC.GetBlocksByRelatedTxAsync(traderet.TxHash);
            Assert.IsTrue(related.Successful(), $"Can't get rleated tx for trade genesis: {related.ResultCode}");
            var blks = related.GetBlocks();
            var tradgen = blks.FirstOrDefault(a => a is OtcTradeGenesisBlock) as OtcTradeGenesisBlock;
            Assert.IsNotNull(tradgen, $"Can't get trade genesis: blks count: {blks.Count()}");
            Assert.AreEqual(trade, tradgen.Trade);
            Assert.AreEqual(OTCTradeStatus.Open, tradgen.OTStatus);

            // verify by api
            var tradeQueryRet = await test2Wallet.RPC.FindOtcTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks().OrderBy(a => a.TimeStamp);
            //Assert.AreEqual(3, tradeQueryResultBlocks.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks.Last() as TransactionBlock).AccountID);

            var tradeQueryRet2 = await testWallet.RPC.FindOtcTradeAsync(testWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet2.Successful(), $"Can't query trade via FindOtcTradeAsync: {tradeQueryRet2.ResultCode}");
            var tradeQueryResultBlocks2 = tradeQueryRet2.GetBlocks().OrderBy(a => a.TimeStamp);
            //Assert.AreEqual(3, tradeQueryResultBlocks2.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks2.Last() as TransactionBlock).AccountID);

            // buyer send payment indicator
            var payindret = await test2Wallet.OTCTradeFiatPaymentSentAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

            await WaitWorkflow("OTCTradeBuyerPaymentSentAsync");
            // status changed to BuyerPaid
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.FiatSent, (trdlatest.GetBlock() as IOtcTrade).OTStatus,
                $"Trade status not changed to BuyerPaid");

            // seller not got the payment. seller raise a dispute
            var crdptret = await testWallet.OTCTradeRaiseDisputeAsync(tradgen.AccountID);
            Assert.IsTrue(crdptret.Successful(), $"Raise dispute failed: {crdptret.ResultCode}");

            await WaitWorkflow("OTCTradeRaiseDisputeAsync");

            // then get the trade, the status should be dispute
            trdlatest = await testWallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(OTCTradeStatus.Dispute, (trdlatest.GetBlock() as IOtcTrade).OTStatus,
                $"Trade status not changed to Dispute");


            //// seller got the payment
            //var gotpayret = await testWallet.OTCTradeSellerGotPaymentAsync(tradgen.AccountID);
            //Assert.IsTrue(payindret.Successful(), $"Got Payment indicator error: {payindret.ResultCode}");

            //await WaitWorkflow("OTCTradeSellerGotPaymentAsync");
            //// status changed to BuyerPaid
            //var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            //Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");
            //Assert.AreEqual(OTCTradeStatus.CryptoReleased, (trdlatest2.GetBlock() as IOtcTrade).OTStatus,
            //    $"Trade status not changed to ProductReleased");

            //await test2Wallet.SyncAsync(null);
            //Assert.AreEqual(test2balance - 13, test2Wallet.BaseBalance, $"Test2 got collateral wrong. should be {test2balance} but {test2Wallet.BaseBalance}");

            //// trade is ok. now its time to close the order
            //var closeret = await testWallet.CloseOTCOrderAsync(dao1.AccountID, otcg.AccountID);
            //Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            //await WaitWorkflow("CloseOTCOrderAsync");
            //var ordfnlret = await testWallet.RPC.GetLastBlockAsync(otcg.AccountID);
            //Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
            //Assert.AreEqual(OTCOrderStatus.Closed, (ordfnlret.GetBlock() as IOtcOrder).OOStatus,
            //    $"Order status not changed to Closed");

            //await testWallet.SyncAsync(null);
            //var lyrshouldbe = testbalance - 10016;
            //Assert.AreEqual(lyrshouldbe, testWallet.BaseBalance, $"Test got collateral wrong. should be {lyrshouldbe} but {testWallet.BaseBalance}");
            //var bal2 = testWallet.GetLastSyncBlock().Balances[crypto].ToBalanceDecimal();
            //Assert.AreEqual(100000m - 1.1m, bal2,
            //    $"testwallet balance of crypto should be {100000m - 1.1m} but {bal2}");

            //await Task.Delay(100);
            //Assert.IsTrue(_authResult, $"Authorizer failed: {_sbAuthResults}");
            //ResetAuthFail();

            return tradgen.AccountID;
        }

        private async Task CreateConsolidation()
        {
            await Task.Delay(1000);
            var lcon = await store.GetLastConsolidationBlockAsync();
            var unConsList = await testWallet.RPC.GetBlockHashesByTimeRangeAsync(lcon.TimeStamp.AddSeconds(18), DateTime.UtcNow);
            await cs.LeaderCreateConsolidateBlockAsync(lcon, DateTime.UtcNow, unConsList.Entities);
            var lcon2 = await testWallet.RPC.GetLastConsolidationBlockAsync();
            Assert.IsTrue(lcon.Height + 1 == lcon2.GetBlock().Height);
        }

        private async Task TestNodeFee()
        {
            // create service block
            var lsb = await testWallet.RPC.GetLastServiceBlockAsync();
            var svcb = await cs.CreateNewViewAsNewLeaderAsync();
            var svcret = await AuthAsync(svcb);
            Assert.IsTrue(svcret.Successful());

            var lsb2 = await testWallet.RPC.GetLastServiceBlockAsync();
            Assert.IsTrue(lsb.GetBlock().Height + 1 == lsb2.GetBlock().Height);

            var lconRet = await testWallet.RPC.GetLastConsolidationBlockAsync();
            Assert.IsTrue(lconRet.Successful());            

            await CreateConsolidation();

            // create a profiting account
            Console.WriteLine("Profiting gen");
            var crpftret = await genesisWallet.CreateProfitingAccountAsync($"moneycow{_rand.Next()}", ProfitingType.Node,
                0.5m, 50);
            Assert.IsTrue(crpftret.Successful());
            var pftblocks = await genesisWallet.GetBrokerAccountsAsync<ProfitingGenesis>();
            var pftblock = pftblocks.FirstOrDefault();
            Assert.IsTrue(pftblock.OwnerAccountId == genesisWallet.AccountId);

            Console.WriteLine("Generate dividends");
            await genesisWallet.CreateDividendsAsync(pftblock.AccountID);
            await Task.Delay(2 * 1000);
        }

        private async Task<IStaking> CreateStaking(Wallet w, string pftid, decimal amount)
        {
            ResetAuthFail();

            var crstkret = await w.CreateStakingAccountAsync($"moneybag{_rand.Next()}", pftid, 30, true);
            Assert.IsTrue(crstkret.Successful());

            var stks = await w.GetBrokerAccountsAsync<StakingGenesis>();
            var stkblock = stks.FirstOrDefault();
            Assert.IsTrue(stkblock.OwnerAccountId == w.AccountId);
            await WaitWorkflow($"CreateStakingAccountAsync {stkblock.RelatedTx}");

            var addstkret = await w.AddStakingAsync(stkblock.AccountID, amount);
            Assert.IsTrue(addstkret.Successful());
            await WaitWorkflow($"AddStakingAsync {addstkret.TxHash}");

            var stk = await w.GetStakingAsync(stkblock.AccountID);
            Assert.AreEqual(amount, (stk as TransactionBlock).Balances["LYR"].ToBalanceDecimal());
            return stk;
        }

        private async Task UnStaking(Wallet w, string stkid)
        {
            var balance = w.BaseBalance;
            var unstkret = await w.UnStakingAsync(stkid);
            Assert.IsTrue(unstkret.Successful(), $"Failed to UnStaking: {unstkret.ResultCode}");
            await WaitWorkflow($"UnStakingAsync {unstkret.TxHash}");
            await w.SyncAsync(null);
            var nb = balance + 2000m - 2;// * 0.988m; // two send fee
            //Assert.AreEqual(nb, w.BaseBalance);

            var stk2 = await w.GetStakingAsync(stkid);
            Assert.AreEqual((stk2 as TransactionBlock).Balances["LYR"].ToBalanceDecimal(), 0);

            var unstkretx = await w.UnStakingAsync(stkid);
            await WaitBlock($"UnStakingAsync {unstkret.TxHash}");
            Assert.IsTrue(!unstkretx.Successful());
        }

        private async Task TestProfitingAndStaking()
        {
            var shareRito = 0.5m;
            var totalProfit = 10000m;

            // create a profiting account
            Console.WriteLine("Profiting gen");
            var crpftret = await testWallet.CreateProfitingAccountAsync($"moneycow{_rand.Next()}", ProfitingType.Node,
                shareRito, 50);
            Assert.IsTrue(crpftret.Successful(), $"Can't create profiting: {crpftret.ResultCode}");
            var pfts = await testWallet.GetBrokerAccountsAsync<ProfitingGenesis>();
            var pftblock = pfts.FirstOrDefault();
            Assert.IsTrue(pftblock.OwnerAccountId == testWallet.AccountId);

            Console.WriteLine("Staking 1");
            // create two staking account, add funds, and vote to it
            var stk = await CreateStaking(testWallet, pftblock.AccountID, 2000m);
            Assert.IsNotNull(stk);

            Console.WriteLine("Staking 2"); 
            var stk2 = await CreateStaking(test2Wallet, pftblock.AccountID, 2000m);
            Assert.IsNotNull(stk2);

            // get the base balance
            await testWallet.SyncAsync(null);
            await test2Wallet.SyncAsync(null);

            Console.WriteLine($"({DateTime.Now:mm:ss.ff}) send as profit");
            // send profit to profit account
            for(var i = 0; i < 1; i++)
            {
                var sendret = await genesisWallet.SendAsync(10000m, pftblock.AccountID);
                Assert.IsTrue(sendret.Successful());
            }

            ResetAuthFail();

            Console.WriteLine($"({DateTime.Now:mm:ss.ff}) Dividend");
            // the owner try to get the dividends
            var getpftRet = await testWallet.CreateDividendsAsync(pftblock.AccountID);
            Assert.IsTrue(getpftRet.Successful(), $"Failed to get dividends: {getpftRet.ResultCode}");
            // then sync wallet and see if it gets a dividend
            await WaitWorkflow("CreateDividendsAsync");

            if (networkId == "devnet")
                await Task.Delay(3000);
            var bal1 = testWallet.BaseBalance;
            Console.WriteLine($"({DateTime.Now:mm:ss.ff}) Check balance");
            await testWallet.SyncAsync(null);
            var delta = testWallet.BaseBalance - bal1;
            //Assert.AreEqual(bal1 + 15000m, testWallet.BaseBalance);

            var bal2 = test2Wallet.BaseBalance;
            await test2Wallet.SyncAsync(null);
            //Assert.AreEqual(bal2 + totalProfit * shareRito / 2, test2Wallet.BaseBalance);

            await UnStaking(testWallet, (stk as TransactionBlock).AccountID);
            await UnStaking(test2Wallet, (stk2 as TransactionBlock).AccountID);
        }

        private async Task TestPoolAsync()
        {
            // create pool
            var token0 = "unnitest/test0";
            var token1 = "unnitest/test1";
            var secs0 = token0.Split('/');
            var result0 = await testWallet.CreateTokenAsync(secs0[1], secs0[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(result0.Successful(), "Failed to create token: " + result0.ResultCode);
            await testWallet.SyncAsync(null);

            var secs1 = token1.Split('/');
            var result1 = await testWallet.CreateTokenAsync(secs1[1], secs1[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
            Assert.IsTrue(result0.Successful(), "Failed to create token: " + result1.ResultCode);
            await testWallet.SyncAsync(null);

            var crplret = await testWallet.CreateLiquidatePoolAsync(token0, "LYR");
            Assert.IsTrue(crplret.Successful(), $"Error create liquidate pool {crplret.ResultCode}");
            await WaitWorkflow("CreateLiquidatePoolAsync");
            var pool = await testWallet.GetLiquidatePoolAsync(token0, "LYR");
            Assert.IsTrue(pool.PoolAccountId != null && pool.PoolAccountId.StartsWith('L'), "Can't get pool created.");

            // add liquidate to pool
            var addpoolret = await testWallet.AddLiquidateToPoolAsync(token0, 1000000, "LYR", 5000);
            Assert.IsTrue(addpoolret.Successful());

            await WaitWorkflow($"AddLiquidateToPoolAsync {addpoolret.TxHash}");

            // swap
            var poolx = await client.GetPoolAsync(token0, LyraGlobal.OFFICIALTICKERCODE);
            Assert.IsNotNull(poolx.PoolAccountId);
            var poolLatestBlock = poolx.GetBlock() as TransactionBlock;

            await testWallet.SyncAsync(null);

            var oldtkn0 = testWallet.GetLastSyncBlock().Balances[token0].ToBalanceDecimal();
            var cal2 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, token0, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, 20, 0);
            var swapret = await testWallet.SwapTokenAsync("LYR", token0, "LYR", 20, cal2.SwapOutAmount);
            Assert.IsTrue(swapret.Successful());
            await WaitWorkflow($"SwapTokenAsync {swapret.TxHash}");

            await testWallet.SyncAsync(null);

            var gotamount = testWallet.GetLastSyncBlock().Balances[token0].ToBalanceDecimal() - oldtkn0;
            Console.WriteLine($"Got swapped amount {gotamount} {token0}");

            // remove liquidate from pool
            var rmliqret = await testWallet.RemoveLiquidateFromPoolAsync(token0, "LYR");
            Assert.IsTrue(rmliqret.Successful());

            await testWallet.SyncAsync(null);
        }

        private async Task TestDealerAsync()
        {
            var url = "https://dealer.devnet.lyra.live:7070";
            dealer = new DealerClient(new Uri(new Uri(url), "/api/dealer/"));
            var dealerAbi = new Wallet.LyraContractABI
            {
                svcReq = BrokerActions.BRK_DLR_CREATE,
                targetAccountId = PoolFactoryBlock.FactoryAccount,
                amounts = new Dictionary<string, decimal>
                    {
                        { LyraGlobal.OFFICIALTICKERCODE, 1 },
                    },
                objArgument = new DealerCreateArgument
                {
                    Name = "first dealer",
                    Description = "a dealer for unit test",
                    ServiceUrl = url,
                    DealerAccountId = testWallet.AccountId,
                    Mode = ClientMode.Permissionless
                }
            };

            // we temp disable the dealer creation.
            var ret = await testWallet.ServiceRequestAsync(dealerAbi);
            Assert.IsTrue(ret.Successful(), $"unable to create dealer: {ret.ResultCode}");
            await WaitWorkflow(ret.TxHash, $"Create Dealer");

            var ret2 = await testWallet.ServiceRequestAsync(dealerAbi);
            Assert.IsTrue(!ret2.Successful(), $"should not to create dealer: {ret2.ResultCode}");
            await WaitBlock($"Create Dealer 2");

            // get dealers
            var gdret = await testWallet.RPC.GetDealerByAccountIdAsync(testWallet.AccountId);
            Assert.IsTrue(gdret.Successful(), $"Can't get dealer: {gdret.ResultCode}");

            dlr = gdret.As<IDealer>();
            Assert.IsNotNull(dlr, "unable to get dealder genesis block");

            // already tested other place
            // register user to dealer
            //var devnetLyra = LyraRestClient.Create("devnet", "", "", "");
            //var lsb = await devnetLyra.GetLastServiceBlockAsync();
            //var regret = await dealer.RegisterAsync(testWallet.AccountId,
            //        "test", "Unit", "", "Test", "t@", "1111", "1111", "",
            //        Signatures.GetSignature(testWallet.PrivateKey, (lsb.GetBlock().Hash), testWallet.AccountId),
            //        "", "");
            //Assert.IsTrue(regret.Successful());

            ResetAuthFail();
        }
    }
}
