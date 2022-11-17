using Converto;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.API.ABI;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Akka.Streams.Attributes;

namespace UnitTests
{
    [TestClass]
    public class UT_UniTrade : XTestBase
    {
        IDealer dlr;

        [TestMethod]
        public async Task TestUniTradeAsync()
        {
            await SetupWallets("xtest");

            #region prepare for trade, generate items
            // dealer is necessary.
            await TestDealerAsync();

            // all trades happened in a DAO
            var daoName = "First DAO";
            var daoDesc = "Doing great business!";

            var dao = await CreateDaoAsync(genesisWallet, daoName, daoDesc);

            var fiatg = await CreateTokenAsync(genesisWallet, "fiat", "USD", "US Dollar", 0);
            var tetherg = await CreateTokenAsync(genesisWallet, "tether", "USDT", "USDT", 10000000);
            Assert.IsTrue((await genesisWallet.SendAsync(10000m, test2PublicKey, tetherg.Ticker)).Successful());

            // create and sell NFT
            var nftg1 = await CreateTestNFTAsync(testWallet);
            Assert.IsNotNull(nftg1);

            var nftg2 = await CreateTestNFTAsync(testWallet);
            Assert.IsNotNull(nftg2);
            #endregion
            var t = CreateTestNFTAsync(test2Wallet);

            Console.WriteLine("Test sell nft OTC to test2 for fiat");
            await TestUniTradeAsync(dao, testWallet, nftg1, test2Wallet, fiatg);

            Console.WriteLine("Test sell nft to test2");
            await TestUniTradeAsync(dao, testWallet, nftg2, test2Wallet, tetherg);

            // after test, dump the database statistics

            await TestChangeDAO();

            //var tradeid = await TestUniTradeDispute();   // test for dispute
            ////await TestVoting(tradeid); // related to dealer. bypass. real test in Uni unit test

            //await TestPoolAsync();
            //await TestProfitingAndStaking();
            //await TestNodeFee();

            //// let workflow to finish
            //await Task.Delay(1000);
            Console.WriteLine(cs.PrintProfileInfo());
        }

        private async Task<TokenGenesisBlock> CreateTokenAsync(Wallet ownerWallet, string domain, string token, 
            string tokenDesc, decimal supply)
        {
            // fiat never means to be hold in wallet.
            var ret = await ownerWallet.CreateTokenAsync(token, domain, tokenDesc, 2, supply, false, 
                null, null, fiat, ContractTypes.FiatCurrency, null);

            Assert.IsTrue(ret.Successful(), $"Token {token} not created properly: {ret.ResultMessage}");

            var find = await ownerWallet.GetTokenGenesisBlockAsync(domain + "/" + token);
            Assert.IsNotNull(find, $"can't find token after fiat genesis");

            return find;
        }

        private async Task<IDao> CreateDaoAsync(Wallet ownerWallet, string daoName, string daoDesc)
        {
            var dcret = await ownerWallet.CreateDAOAsync(daoName, daoDesc, 1, 0.01m, 0.001m, 10, 120, 130);
            Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");
            await WaitWorkflow("CreateDAOAsync");
            var daoret = await testWallet.RPC.GetDaoByNameAsync(daoName);
            Assert.IsTrue(daoret.Successful(), $"Can't get DAO: {daoret.ResultCode}");
            var daoblk = daoret.GetBlock() as IDao;
            Assert.AreEqual(daoName, daoblk.Name);
            Assert.AreEqual(daoDesc, daoblk.Description);
            Assert.AreEqual(1, daoblk.ShareRito);
            Assert.AreEqual(0.01m, daoblk.SellerFeeRatio);
            Assert.AreEqual(0.001m, daoblk.BuyerFeeRatio);
            Assert.AreEqual(120, daoblk.SellerPar);
            Assert.AreEqual(130, daoblk.BuyerPar);

            // test getalldao api
            var alldaoret = await testWallet.RPC.GetAllDaosAsync(0, 10);
            Assert.IsTrue(alldaoret.Successful(), $"can get all dao: {alldaoret.ResultCode}");
            var daos = alldaoret.GetBlocks();
            Assert.AreEqual(1, daos.Count(), $"can't find dao by GetAllDaosAsync");
            var dao0 = alldaoret.GetBlocks().First() as DaoGenesisBlock;
            //Assert.IsTrue(daoblk.AuthCompare(dao0));

            // get dao by the IBroker api
            var brkblksret = await ownerWallet.RPC.GetAllBrokerAccountsForOwnerAsync(ownerWallet.AccountId);
            Assert.IsTrue(brkblksret.Successful(), $"Can't get DAO by brk api: {brkblksret.ResultCode}");
            var daoblk2 = brkblksret.GetBlocks().FirstOrDefault(a => a is DaoGenesisBlock) as DaoGenesisBlock;
            Assert.AreEqual(daoName, daoblk2.Name);
            Assert.AreEqual(daoDesc, daoblk2.Description);

            var dao1 = daoret.GetBlock() as IDao;
            return dao1;
        }

        private async Task TestChangeDAO()
        {
            // create a DAO for nodes
            var name = "Node Owners Club";
            var desc = "Doing great business!";
            var dcret = await genesisWallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.005m, 10, 120, 120);
            Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

            await WaitWorkflow("CreateDAOAsync");

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
            Assert.AreEqual(UniTradeStatus.Dispute, (trdlatest.GetBlock() as IUniTrade).UTStatus,
                $"Trade statust is not dispute");
            var trade = trdlatest.GetBlock() as IUniTrade;
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
                Type = SubjectType.UniDispute,
                DaoId = trade.Trade.daoId,
                Issuer = testWallet.AccountId,
                TimeSpan = 100,
                Title = "Now let vote on case ID 111",
                Description = "bla bla bla",
                Options = new [] { "Yay", "Nay"},
            };

            var resolution = new ODRResolution
            {
                RType = ResolutionType.UniTrade,
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
            var latestTrade = latestTradeRet.GetBlock() as IUniTrade;
            Assert.AreEqual(UniTradeStatus.Dispute, latestTrade.UTStatus);

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
            latestTrade = latestTradeRet.GetBlock() as IUniTrade;
            Assert.AreEqual(UniTradeStatus.DisputeClosed, latestTrade.UTStatus);

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

        private async Task TestUniTradeAsync(IDao dao, 
            Wallet offeringWallet, TokenGenesisBlock offeringGen,
            Wallet bidingWallet, TokenGenesisBlock bidingGen)
        {
            Assert.IsNotNull(offeringGen, "propGen should not be null");
            Assert.IsNotNull(dao, "dao should not be null");
            Assert.IsNotNull(dealer, "dealer should not be null");

            // calculate fees
            await offeringWallet.SyncAsync();            
            var offeringBalanceInput = offeringWallet.BaseBalance;
            await bidingWallet.SyncAsync();
            var bidingBalanceInput = bidingWallet.BaseBalance;
            // after trading, the balance should be
            var collateralCount = 100_000;
            var offeringBalanceShouldBe = offeringBalanceInput 
                - (bidingGen.DomainName == "fiat" ? 3 : -1)        // sending fee
                - LyraGlobal.GetListingFeeFor(LyraGlobal.GetHoldTypeFromTicker(offeringGen.Ticker))
                - collateralCount * (LyraGlobal.OfferingNetworkFeeRatio + dao.SellerFeeRatio);
            var bidingBalanceShouldBe = bidingBalanceInput
                - (bidingGen.DomainName == "fiat" ? 3 : 1)         // sending fee
                - collateralCount * (LyraGlobal.BidingNetworkFeeRatio + dao.BuyerFeeRatio);
            var offeringBalanceTokenInput = offeringWallet.GetLastSyncBlock().Balances[offeringGen.Ticker].ToBalanceDecimal();
            var bidingBalanceTokenInput = bidingWallet.GetLastSyncBlock().Balances.ContainsKey(offeringGen.Ticker) ?
                bidingWallet.GetLastSyncBlock().Balances[offeringGen.Ticker].ToBalanceDecimal() : 0;

            var daolatest = (await offeringWallet.RPC.GetLastBlockAsync(dao.AccountID)).As<TransactionBlock>();
            var daoBalanceInput = daolatest.Balances.ContainsKey("LYR") ? daolatest.Balances["LYR"].ToBalanceDecimal() : 0;
            var daoBalanceShouldBe = daoBalanceInput
                + LyraGlobal.GetListingFeeFor(LyraGlobal.GetHoldTypeFromTicker(offeringGen.Ticker))
                + collateralCount * dao.SellerFeeRatio
                + collateralCount * dao.BuyerFeeRatio
                + (bidingGen.DomainName == "fiat" ? -1 : -2)         // a send
                ;

            Assert.IsTrue(offeringWallet.GetLastSyncBlock().Balances[offeringGen.Ticker] > 0);
            if (bidingGen.DomainName != "fiat")
                Assert.IsTrue(bidingWallet.GetLastSyncBlock().Balances[bidingGen.Ticker] > 0);

            await PrintBalancesForAsync(offeringWallet.AccountId, bidingWallet.AccountId,
                    dao.AccountID);

            var crypto = offeringGen.Ticker;

            await offeringWallet.SyncAsync(null);
            var testbalance = offeringWallet.BaseBalance;

            //var prices = await dealer.GetPricesAsync();
            var order = new UniOrder
            {
                daoId = dao.AccountID,
                dealerId = dlr.AccountID,
                offerby = LyraGlobal.GetHoldTypeFromTicker(offeringGen.Ticker),
                offering = offeringGen.Ticker,
                bidby = LyraGlobal.GetHoldTypeFromTicker(bidingGen.Ticker),
                biding = bidingGen.Ticker,
                price = 2000,
                cltamt = collateralCount,
                payBy = new string[] { "Paypal" },

                amount = 1,
                limitMin = 200,
                limitMax = 2000,
            };

            var ret = await offeringWallet.CreateUniOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"Can't create order: {ret.ResultCode}");

            await WaitWorkflow($"CreateUniOrderAsync");

            await DaoTraeasureShouldBe(dao, daoBalanceInput + collateralCount + 98);

            var Uniret = await offeringWallet.RPC.GetUniOrdersByOwnerAsync(offeringWallet.AccountId);
            Assert.IsTrue(Uniret.Successful(), $"Can't get Uni gensis block. {Uniret.ResultCode}");
            var Unis = Uniret.GetBlocks();
            Assert.IsTrue(Unis.First() is IUniOrder, $"Uni order gensis block not found.");

            // test find tradable orders
            var tradableret = await offeringWallet.RPC.FindTradableUniAsync();
            Assert.IsTrue(tradableret.Successful(), "Unable to find tradable.");
            var tradableblks = tradableret.GetBlocks("orders");
            Assert.AreEqual(1, tradableblks.Count(), $"Trade tradable block count is {tradableblks.Count()}");
            var firsttradable = tradableblks.First();
            Assert.IsTrue(firsttradable is IUniOrder fodr && fodr.Name == "no name");

            // then DAO treasure should not have the crypto
            var daoret3 = await offeringWallet.RPC.GetDaoByNameAsync(dao.Name);
            Assert.IsTrue(daoret3.Successful(), $"Can't get DAO: {daoret3.ResultCode}");
            var daot = daoret3.GetBlock() as TransactionBlock;

            //if(direction == TradeDirection.Sell)
            //{
            //    Assert.IsTrue(daot.Balances.ContainsKey(crypto), "No collateral token in DAO treasure.");
            //    Assert.AreEqual(0, daot.Balances[crypto].ToBalanceDecimal());
            //}
            //else
            //{
            //    Assert.IsTrue(!daot.Balances.ContainsKey(crypto), "collateral token should not in DAO treasure.");
            //    Assert.AreEqual(0, daot.Balances[crypto].ToBalanceDecimal());
            //}

            var Unig = Unis.Last() as UniOrderGenesisBlock;
            Assert.IsTrue(order.Equals(Unig.Order), "Uni order not equal.");

            await PrintBalancesForAsync(offeringWallet.AccountId, bidingWallet.AccountId,
                dao.AccountID, Unig.AccountID);

            await test2Wallet.SyncAsync(null);
            var test2balance = test2Wallet.BaseBalance;
            var tradgen = await CreateUniTradeAsync(dao, testWallet, test2Wallet, Unig, collateralCount);

            if (bidingGen.Ticker.StartsWith("fiat/"))
            {
                //await CancelUniTrade(test2Wallet, tradgen);
                //await test2Wallet.SyncAsync(null);
                //var test2balanceA = test2Wallet.BaseBalance;
                //Assert.AreEqual(test2balance - 3m, test2balanceA, "Balance not ok after cancel trade.");

                //tradgen = await CreateUniTradeAsync(dao, testWallet, test2Wallet, Unig, collateralCount);
                // cancel one

                // buyer send payment indicator
                var wlt = test2Wallet;
                AuthorizationAPIResult payindret = await wlt.UniTradeFiatPaymentSentAsync(tradgen.AccountID);
                Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

                await WaitWorkflow($"UniTradeBuyerPaymentSentAsync");
                // status changed to BuyerPaid
                var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
                Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
                Assert.AreEqual(UniTradeStatus.BidSent, (trdlatest.GetBlock() as IUniTrade).UTStatus,
                    $"Trade statust not changed to BuyerPaid");

                // seller got the payment
                var wlt2 = offeringWallet;
                var gotpayret = await wlt2.UniTradeFiatPaymentConfirmAsync(tradgen.AccountID);
                Assert.IsTrue(gotpayret.Successful(), $"Got Payment indicator error: {payindret.ResultCode}");

                await WaitWorkflow($"UniTradeSellerGotPaymentAsync");

                // status changed to BuyerPaid
                var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
                Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");
                Assert.AreEqual(UniTradeStatus.OfferReceived, (trdlatest2.GetBlock() as IUniTrade).UTStatus,
                    $"Trade status not changed to ProductReleased");

                // trade is ok. now its time to close the order
                var closeret = await offeringWallet.CloseUniOrderAsync(dao.AccountID, Unig.AccountID);
                Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

                await WaitWorkflow($"CloseUniOrderAsync");
                var ordfnlret = await offeringWallet.RPC.GetLastBlockAsync(Unig.AccountID);
                Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
                Assert.AreEqual(UniOrderStatus.Closed, (ordfnlret.GetBlock() as IUniOrder).UOStatus,
                    $"Order status not changed to Closed: {(ordfnlret.GetBlock() as IUniOrder).UOStatus}");
                Assert.AreEqual(0, (ordfnlret.GetBlock() as TransactionBlock).Balances["LYR"], "LYR not zero");
            }

            await test2Wallet.SyncAsync(null);

            // buyer fee calculated as LYR
            var totalAmount = tradgen.Trade.amount;
            decimal totalFee = 0;
            var trade = tradgen.Trade;
            // transaction fee

            //if (trade.dir == TradeDirection.Sell)
            //{
            //    totalFee += Math.Round((((totalAmount * trade.price) * order.fiatPrice) * dao.SellerFeeRatio) / order.collateralPrice, 8);
            //}
            //else
            //{
            //    totalFee += Math.Round((((totalAmount * trade.price) * order.fiatPrice) * dao.BuyerFeeRatio) / order.collateralPrice, 8);
            //}

            //// network fee
            //var networkFee = Math.Round((((totalAmount * order.price) * order.fiatPrice) * 0.002m) / order.collateralPrice, 8);

            //var buyerfee = 0;// totalFee + networkFee;
            ////Console.WriteLine($"Cost calculated txfee: {totalFee} netfee: {networkFee} Real: {test2balance - test2Wallet.BaseBalance} should be: {totalFee + networkFee + 26}");
            //var buyershouldget = test2balance - 13 - buyerfee - 13;
            //// create trade 10 lyr, send confirm 1, fee 2, cancel 13
            //Assert.AreEqual(buyershouldget, test2Wallet.BaseBalance, $"Test2 got collateral wrong. should be {buyershouldget} but {test2Wallet.BaseBalance} diff {buyershouldget - test2Wallet.BaseBalance}");

            // delist the order (only when amount > 1)
            //Console.WriteLine($"Delisting order: {Unig.AccountID}");
            //var dlret = await offeringWallet.DelistUniOrderAsync(dao.AccountID, Unig.AccountID);
            //Assert.IsTrue(dlret.Successful(), $"Unable to delist order: {dlret.ResultCode}");
            //await WaitWorkflow($"DelistUniOrderAsync");

            //var orddlret = await offeringWallet.RPC.GetLastBlockAsync(Unig.AccountID);
            //Assert.IsTrue(orddlret.Successful(), $"Can't get order latest block: {orddlret.ResultCode}");
            //Assert.AreEqual(UniOrderStatus.Delist, (orddlret.GetBlock() as IUniOrder).UOStatus,
            //    $"Order status not changed to Delisted");

            // trade is ok. now its time to close the order
            //var closeret = await offeringWallet.CloseUniOrderAsync(dao.AccountID, Unig.AccountID);
            //Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            //await WaitWorkflow($"CloseUniOrderAsync");
            //var ordfnlret = await offeringWallet.RPC.GetLastBlockAsync(Unig.AccountID);
            //Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
            //Assert.AreEqual(UniOrderStatus.Closed, (ordfnlret.GetBlock() as IUniOrder).UOStatus,
            //    $"Order status not changed to Closed: {(ordfnlret.GetBlock() as IUniOrder).UOStatus}");
            //Assert.AreEqual(0, (ordfnlret.GetBlock() as TransactionBlock).Balances["LYR"], "LYR not zero");

            //await offeringWallet.SyncAsync(null);

            //if (order.dir == TradeDirection.Sell)
            //{
            //    totalFee = Math.Round((((totalAmount * order.price) * order.fiatPrice) * dao.SellerFeeRatio) / order.collateralPrice, 8);
            //}
            //else
            //{
            //    totalFee = Math.Round((((totalAmount * order.price) * order.fiatPrice) * dao.BuyerFeeRatio) / order.collateralPrice, 8);
            //}

            //var networkfeeToPay = Math.Round((((2000m * 0.1m) * order.fiatPrice) * 0.002m) / order.collateralPrice, 8);
            var lyrshouldbe = testbalance - 10000 - 10 - 4 - totalFee;// - networkfeeToPay;
            // mint, create order, 4 send, 1 LYR for close order

            //var firstTime = false;
            //if (!firstTime)
            //    lyrshouldbe += 10000;

            //Assert.AreEqual(lyrshouldbe, offeringWallet.BaseBalance, $"Test got collateral wrong. should be {lyrshouldbe} but {offeringWallet.BaseBalance} diff {lyrshouldbe - offeringWallet.BaseBalance}");
            //var bal2 = offeringWallet.GetLastSyncBlock().Balances[crypto].ToBalanceDecimal();

            //decimal x = firstTime ? 0.1m : 0;
            //Assert.AreEqual(100000m - x - 100, bal2,
            //    $"Trade after {direction} ownerWallet balance of crypto should be {100010m - x - 100} but {bal2}");

            // dao should be kept

            await PrintBalancesForAsync(offeringWallet.AccountId, bidingWallet.AccountId,
                dao.AccountID, Unig.AccountID, tradgen.AccountID);

            await Task.Delay(100);
            Assert.IsTrue(_authResult, $"Authorizer failed: {_sbAuthResults}");
            ResetAuthFail();

            await offeringWallet.SyncAsync();
            var offeringBalanceOut = offeringWallet.BaseBalance;
            Assert.AreEqual(offeringBalanceShouldBe, offeringBalanceOut, $"Offering wallet balance is not right, diff: {offeringBalanceOut - offeringBalanceShouldBe}");
            
            await bidingWallet.SyncAsync();
            var bidingBalanceOut = bidingWallet.BaseBalance;
            Assert.AreEqual(bidingBalanceShouldBe, bidingBalanceOut, $"Biding wallet balance is not right, diff: {bidingBalanceOut - bidingBalanceShouldBe}");

            var offeringBalanceTokenOut = offeringWallet.GetLastSyncBlock().Balances[offeringGen.Ticker].ToBalanceDecimal();
            var bidingBalanceTokenOut = bidingWallet.GetLastSyncBlock().Balances.ContainsKey(offeringGen.Ticker) ?
                bidingWallet.GetLastSyncBlock().Balances[offeringGen.Ticker].ToBalanceDecimal() : 0;
            Assert.AreEqual(offeringBalanceTokenInput - 1, offeringBalanceTokenOut);
            Assert.AreEqual(bidingBalanceTokenInput + 1, bidingBalanceTokenOut);

            var daolatest2 = (await offeringWallet.RPC.GetLastBlockAsync(dao.AccountID)).As<TransactionBlock>();
            var daoBalanceOutput = daolatest2.Balances["LYR"].ToBalanceDecimal();
            Assert.AreEqual(daoBalanceShouldBe, daoBalanceOutput, $"Dao treasure balance is not right, diff: {daoBalanceOutput - daoBalanceShouldBe} dao addr: {daolatest2.AccountID}");
        }

        private async Task DaoTraeasureShouldBe(IDao dao, decimal amount)
        {
            var daolatest = (await genesisWallet.RPC.GetLastBlockAsync(dao.AccountID)).As<TransactionBlock>();
            var daoBalanceOutput = daolatest.Balances["LYR"].ToBalanceDecimal();
            Assert.AreEqual(amount, daoBalanceOutput, $"Dao balance not OK! {amount - daoBalanceOutput}");
        }

        private async Task CancelUniTrade(Wallet ownerWallet, UniTradeGenesisBlock tradgen)
        {
            var tradeStatusShouldBe = UniTradeStatus.Open;
            if (tradgen.Trade.bidby == HoldTypes.Token || tradgen.Trade.bidby == HoldTypes.NFT)
            {
                tradeStatusShouldBe = UniTradeStatus.BidReceived;
            }
            // make sure the status of trade is Open or BidRecived
            Assert.AreEqual(tradeStatusShouldBe, tradgen.UTStatus, "Wrong trade status");
            
            var cloret = await ownerWallet.CancelUniTradeAsync(tradgen.Trade.daoId, tradgen.Trade.orderId, tradgen.AccountID);
            // check locked IDs
            await WaitBlock("CancelUniTradeAsync");
            Assert.IsTrue(cloret.Successful(), $"cancel failed: {cloret.ResultCode}");

            Assert.AreEqual(3, _lastAuthResult.LockedIDs.Count, "ID not locked properly");
            Assert.IsTrue(_lastAuthResult.LockedIDs.Contains(tradgen.Trade.daoId));
            Assert.IsTrue(_lastAuthResult.LockedIDs.Contains(tradgen.Trade.orderId));
            Assert.IsTrue(_lastAuthResult.LockedIDs.Contains(tradgen.AccountID));

            // try lock it
            var cloret2 = await ownerWallet.CancelUniTradeAsync(tradgen.Trade.daoId, tradgen.Trade.orderId, tradgen.AccountID);
            await WaitBlock("CancelUniTradeAsync 2");
            Assert.AreEqual(APIResultCodes.ResourceIsBusy, cloret2.ResultCode, $"Not locked properly: {cloret2.ResultCode}");

            await WaitBlock("CancelUniTradeAsync");
            await WaitWorkflow("CancelUniTradeAsync 2", false);

            ResetAuthFail();

            Assert.IsTrue(cloret.Successful(), $"Unable to cancel trade: {cloret.ResultCode}");

            // make sure the status of trade is Closed
            var latestret = await ownerWallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(latestret.Successful());
            var tradelst = latestret.GetBlock() as IUniTrade;
            Assert.AreEqual(UniTradeStatus.Canceled, tradelst.UTStatus, "not close trade properly");
        }

        private async Task<UniTradeGenesisBlock> CreateUniTradeAsync(IDao dao, Wallet offeringWallet, Wallet bidingWallet, UniOrderGenesisBlock Unig, 
            int callateralCount)
        {
            Console.WriteLine("Calling CreateUniTradeAsync");
            var dao1 = dao as TransactionBlock;
            // here comes a buyer, he who want to buy 1 BTC.
            var tradableret = await bidingWallet.RPC.FindTradableUniAsync();
            Assert.IsTrue(tradableret.Successful(), $"Can't find tradableorders: {tradableret.ResultCode}: {tradableret.ResultMessage}");
            var ords = tradableret.GetBlocks("orders");
            Assert.AreEqual(1, ords.Count(), $"Order count not right: {ords.Count()}");
            //Assert.IsTrue((ords.First() as IUniOrder).Order.Equals(order), "Uni order not equal.");

            var fiatg = (await bidingWallet.RPC.GetTokenGenesisBlockAsync("", Unig.Order.biding, "")).As<TokenGenesisBlock>();

            var trade = new UniTrade
            {
                daoId = dao1.AccountID,
                dealerId = Unig.Order.dealerId,
                orderId = Unig.AccountID,
                orderOwnerId = Unig.OwnerAccountId,
                offby = LyraGlobal.GetHoldTypeFromTicker(Unig.Order.offering),
                offering = Unig.Order.offering,
                bidby = LyraGlobal.GetHoldTypeFromTicker(fiatg.Ticker),
                biding = fiatg.Ticker,
                price = 2000,
                
                cltamt = callateralCount,
                payVia = "Paypal",
                amount = 1,
                pay = 2000,
            };

            var traderet = await bidingWallet.CreateUniTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"Create Uni Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow($"CreateUniTradeAsync for sell");
            //if(trade.biding.StartsWith("tether"))
            //{
            //    await Task.Delay(10000000);
            //    Assert.Fail();
            //}

            // the Uni order should now be amount 9
            var Uniret2 = await offeringWallet.RPC.GetUniOrdersByOwnerAsync(offeringWallet.AccountId);
            Assert.IsTrue(Uniret2.Successful(), $"Can't get Uni block. {Uniret2.ResultCode}");
            var Unis2 = Uniret2.GetBlocks();
            Assert.IsTrue(Unis2.Last() is IUniOrder, $"Uni block count not = 1.");
            var Uniorderx = Unis2.Last() as IUniOrder;

            //if(direction == TradeDirection.Buy)
            //    Assert.IsTrue(0.9m == Uniorderx.Order.amount, "order not processed");
            //Assert.AreEqual(0.9m, Uniorderx.Order.amount, "order not processed");

            // get trade
            var related = await bidingWallet.RPC.GetBlocksByRelatedTxAsync(traderet.TxHash);
            Assert.IsTrue(related.Successful(), $"Can't get rleated tx for trade genesis: {related.ResultCode}");
            var blks = related.GetBlocks();
            var tradgen = blks.LastOrDefault(a => a is UniTradeGenesisBlock) as UniTradeGenesisBlock;
            Assert.IsNotNull(tradgen, $"Can't get trade genesis: blks count: {blks.Count()}");
            Assert.AreEqual(trade, tradgen.Trade);

            var tradeStatusShouldBe = UniTradeStatus.Open;
            if (trade.bidby == HoldTypes.Token || trade.bidby == HoldTypes.NFT)
            {
                tradeStatusShouldBe = UniTradeStatus.OfferReceived;
            }
            else
            {
                Assert.AreEqual(UniTradeStatus.Open, tradgen.UTStatus);
            }                

            // verify by api
            var tradeQueryRet = await bidingWallet.RPC.FindUniTradeAsync(bidingWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindUniTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks();
            Assert.IsTrue(tradeQueryResultBlocks.Count() >= 1);
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks
                .OrderBy(a => a.TimeStamp)
                .Last() as TransactionBlock).AccountID);

            var tradeQueryRet2 = await offeringWallet.RPC.FindUniTradeAsync(offeringWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet2.Successful(), $"Can't query trade via FindUniTradeAsync: {tradeQueryRet2.ResultCode}");
            var tradeQueryResultBlocks2 = tradeQueryRet2.GetBlocks();
            //Assert.AreEqual(1, tradeQueryResultBlocks2.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks2
                .OrderBy(a => a.TimeStamp)
                .Last() as TransactionBlock).AccountID);

            var tradeQueryRet3 = await offeringWallet.RPC.FindUniTradeByStatusAsync(dao1.AccountID, tradeStatusShouldBe, 0, 10);
            Assert.IsTrue(tradeQueryRet3.Successful(), $"Can't query trade via FindUniTradeByStatusAsync: {tradeQueryRet3.ResultCode}");
            var tradeQueryResultBlocks3 = tradeQueryRet3.GetBlocks();
            //Assert.AreEqual(1, tradeQueryResultBlocks3.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks3.Last() as TransactionBlock).AccountID);

            return tradgen;
        }

        private async Task<string> TestUniTradeDispute()
        {
            var crypto = "tether/ETH";
            // init. create token to sell
            //var tokenGenesisResult = await testWallet.CreateTokenAsync("ETH", "unittest", "", 8, 100000, false, testWallet.AccountId,
            //        "", "", ContractTypes.Cryptocurrency, null);
            //Assert.IsTrue(tokenGenesisResult.Successful(), $"test Uni token genesis failed: {tokenGenesisResult.ResultCode}");

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
            var order = new UniOrder
            {
                daoId = dao1.AccountID,
                dealerId = dlr.AccountID,
                //crypto = crypto,
                //fiat = fiat,
                //fiatPrice = prices[fiat.ToLower()],
                //priceType = PriceType.Fixed,
                //price = 2000,
                //amount = 2,
                //collateral = 180000000,
                //collateralPrice = prices["LYR"],
                payBy = new string[] { "Paypal" },
                limitMin = 200,
                limitMax = 1000,
            };

            var ret = await testWallet.CreateUniOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"Can't create order: {ret.ResultCode}");

            await WaitWorkflow($"CreateUniOrderAsync dispute sell");

            var Uniret = await testWallet.RPC.GetUniOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(Uniret.Successful(), $"Can't get Uni gensis block. {Uniret.ResultCode}");
            var Unis = Uniret.GetBlocks();
            Assert.IsTrue(Unis.Last() is UniOrderGenesisBlock, $"Uni order gensis block not found.");

            // then DAO treasure should not have the crypto
            var daoret3 = await testWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(daoret3.Successful(), $"Can't get DAO: {daoret3.ResultCode}");
            var daot = daoret3.GetBlock() as TransactionBlock;
            Assert.IsTrue(daot.Balances.ContainsKey(crypto), "No collateral token in DAO treasure.");
            Assert.AreEqual(0, daot.Balances[crypto].ToBalanceDecimal());

            var Unig = Unis.Last() as UniOrderGenesisBlock;
            Assert.IsTrue(order.Equals(Unig.Order), "Uni order not equal.");

            // here comes a buyer, he who want to buy 1 BTC.
            var tradableret = await testWallet.RPC.FindTradableUniAsync();
            Assert.IsTrue(tradableret.Successful(), $"Can't find tradableorders: {tradableret.ResultCode}: {tradableret.ResultMessage}");
            var ords = tradableret.GetBlocks("orders");
            Assert.AreEqual(1, ords.Count(), "Order count not right");
            Assert.IsTrue((ords.First() as IUniOrder).Order.Equals(order), "Uni order not equal.");

            var trade = new UniTrade
            {
                daoId = dao1.AccountID,
                dealerId = Unig.Order.dealerId,
                orderId = Unig.AccountID,
                orderOwnerId = Unig.OwnerAccountId,
                //crypto = "tether/ETH",
                //fiat = fiat,
                //price = 2000,
                //amount = 0.1m,
                //collateral = 40000000,
                pay = 200,
                payVia = "Paypal",
            };
            await test2Wallet.SyncAsync(null);
            var test2balance = test2Wallet.BaseBalance;
            var traderet = await test2Wallet.CreateUniTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"Uni Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow("CreateUniTradeAsync");
            // the Uni order should now be amount 9
            var Uniret2 = await testWallet.RPC.GetUniOrdersByOwnerAsync(testWallet.AccountId);
            Assert.IsTrue(Uniret2.Successful(), $"Can't get Uni block. {Uniret2.ResultCode}");
            var Unis2 = Uniret2.GetBlocks();
            Assert.IsTrue(Unis2.Last() is IUniOrder, $"Uni block count not = 1.");
            var Uniorderx = Unis2.Last() as IUniOrder;
            Assert.AreEqual(1.9m, Uniorderx.Order.amount, "order not processed");

            // get trade
            var related = await test2Wallet.RPC.GetBlocksByRelatedTxAsync(traderet.TxHash);
            Assert.IsTrue(related.Successful(), $"Can't get rleated tx for trade genesis: {related.ResultCode}");
            var blks = related.GetBlocks();
            var tradgen = blks.FirstOrDefault(a => a is UniTradeGenesisBlock) as UniTradeGenesisBlock;
            Assert.IsNotNull(tradgen, $"Can't get trade genesis: blks count: {blks.Count()}");
            Assert.AreEqual(trade, tradgen.Trade);
            Assert.AreEqual(UniTradeStatus.Open, tradgen.UTStatus);

            // verify by api
            var tradeQueryRet = await test2Wallet.RPC.FindUniTradeAsync(test2Wallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindUniTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks().OrderBy(a => a.TimeStamp);
            //Assert.AreEqual(3, tradeQueryResultBlocks.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks.Last() as TransactionBlock).AccountID);

            var tradeQueryRet2 = await testWallet.RPC.FindUniTradeAsync(testWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet2.Successful(), $"Can't query trade via FindUniTradeAsync: {tradeQueryRet2.ResultCode}");
            var tradeQueryResultBlocks2 = tradeQueryRet2.GetBlocks().OrderBy(a => a.TimeStamp);
            //Assert.AreEqual(3, tradeQueryResultBlocks2.Count());
            Assert.AreEqual(tradgen.AccountID, (tradeQueryResultBlocks2.Last() as TransactionBlock).AccountID);

            // buyer send payment indicator
            var payindret = await test2Wallet.UniTradeFiatPaymentSentAsync(tradgen.AccountID);
            Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

            await WaitWorkflow("UniTradeBuyerPaymentSentAsync");
            // status changed to BuyerPaid
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(UniTradeStatus.BidSent, (trdlatest.GetBlock() as IUniTrade).UTStatus,
                $"Trade status not changed to BuyerPaid");

            // seller not got the payment. seller raise a dispute
            var crdptret = await testWallet.UniTradeRaiseDisputeAsync(tradgen.AccountID);
            Assert.IsTrue(crdptret.Successful(), $"Raise dispute failed: {crdptret.ResultCode}");

            await WaitWorkflow("UniTradeRaiseDisputeAsync");

            // then get the trade, the status should be dispute
            trdlatest = await testWallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(UniTradeStatus.Dispute, (trdlatest.GetBlock() as IUniTrade).UTStatus,
                $"Trade status not changed to Dispute");


            //// seller got the payment
            //var gotpayret = await testWallet.UniTradeSellerGotPaymentAsync(tradgen.AccountID);
            //Assert.IsTrue(payindret.Successful(), $"Got Payment indicator error: {payindret.ResultCode}");

            //await WaitWorkflow("UniTradeSellerGotPaymentAsync");
            //// status changed to BuyerPaid
            //var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            //Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");
            //Assert.AreEqual(UniTradeStatus.CryptoReleased, (trdlatest2.GetBlock() as IUniTrade).OTStatus,
            //    $"Trade status not changed to ProductReleased");

            //await test2Wallet.SyncAsync(null);
            //Assert.AreEqual(test2balance - 13, test2Wallet.BaseBalance, $"Test2 got collateral wrong. should be {test2balance} but {test2Wallet.BaseBalance}");

            //// trade is ok. now its time to close the order
            //var closeret = await testWallet.CloseUniOrderAsync(dao1.AccountID, Unig.AccountID);
            //Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

            //await WaitWorkflow("CloseUniOrderAsync");
            //var ordfnlret = await testWallet.RPC.GetLastBlockAsync(Unig.AccountID);
            //Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
            //Assert.AreEqual(UniOrderStatus.Closed, (ordfnlret.GetBlock() as IUniOrder).OOStatus,
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
            var pftblock = crpftret.GetBlock() as ProfitingBlock;
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

            var stkblock = crstkret.GetBlock() as StakingBlock;
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
            var pftblock = crpftret.GetBlock() as ProfitingBlock;
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
            await WaitWorkflow($"Create Dealer");

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

        public async Task<TokenGenesisBlock> CreateTestNFTAsync(Wallet ownerWallet)
        {
            //var ticker = "nft/a346b16b-ca6c-4c86-9519-1e72fd517e9B";
            //var retg = await ownerWallet.RPC.GetTokenGenesisBlockAsync(testPublicKey, ticker, "aaa");
            //Assert.IsTrue(retg.Successful());
            //return;
            //await BurnAllNFT();
            //return;

            var metauri = "https://lyra.live/meta/some";
            var rand = new Random();

            var name = $"a great nft ({rand.NextInt64()})";
            var ret = await ownerWallet.CreateNFTAsync(name, "a nft for unit test", 10, metauri);
            Assert.IsTrue(ret.Successful(), $"Create NFT failed: {ret.ResultMessage}");

            // send
            var nftgen = ownerWallet.GetLastSyncBlock() as TokenGenesisBlock;
            Assert.IsNotNull(nftgen);
            Assert.AreEqual(name, nftgen.Custom1);

            var tickrToSend = nftgen.Ticker;
            var findSendRet = await ownerWallet.RPC.FindNFTGenesisSendAsync(testPublicKey, nftgen.Ticker, "0");
            Assert.AreEqual(APIResultCodes.BlockNotFound, findSendRet.ResultCode);

            var nft = ownerWallet.IssueNFT(nftgen.Ticker, null);
            var amounts = new Dictionary<string, decimal>
            {
                { nftgen.Ticker, 1m }
            };
            var sendRet = await ownerWallet.SendExAsync(test3PublicKey, amounts, null, nft);
            Assert.IsTrue(sendRet.Successful(), $"Faid to send NFT: {sendRet.ResultCode}");
            var sendBlock = ownerWallet.GetLastSyncBlock();

            // then test3 will receive it.
            await test3Wallet.SyncAsync();
            var recvBlockx = test3Wallet.GetLastSyncBlock();
            Assert.IsTrue(recvBlockx is ReceiveTransferBlock, "not a receive block");
            var recvBlock = recvBlockx as ReceiveTransferBlock;
            Assert.IsTrue(recvBlock.SourceHash == sendBlock.Hash, "not receive properly");
            Assert.IsTrue(recvBlock.Balances.ContainsKey(tickrToSend));
            Assert.IsTrue(recvBlock.Balances[tickrToSend] == 1m.ToBalanceLong());

            // then test3 will send to test4
            var send2ret = await test3Wallet.SendAsync(1m, test4PublicKey, nftgen.Ticker);
            Assert.IsTrue(send2ret.Successful(), $"Faid to send NFT to test3: {send2ret.ResultCode}");

            await test4Wallet.SyncAsync();
            var recvBlockx2 = test4Wallet.GetLastSyncBlock();
            Assert.IsTrue(recvBlockx2 is ReceiveTransferBlock, "not a receive block");
            var recvBlock2 = recvBlockx2 as ReceiveTransferBlock;
            Assert.IsTrue(recvBlock2.SourceHash == send2ret.TxHash, "not receive properly");
            Assert.IsTrue(recvBlock2.Balances.ContainsKey(tickrToSend));
            Assert.IsTrue(recvBlock2.Balances[tickrToSend] == 1m.ToBalanceLong());

            return nftgen;
            //await BurnAllNFT();
            //name = $"a great nft ({rand.NextInt64()})";
            //ret = await ownerWallet.CreateNFTAsync(name, "a nft for unit test", 10, metauri);
            //Assert.IsTrue(ret.Successful(), $"Create NFT failed: {ret.ResultMessage}");
        }
    }
}
