﻿using Converto;
using Loyc.Collections;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.WorkFlow;
using Lyra.Data.API;
using Lyra.Data.API.ABI;
using Lyra.Data.API.ODR;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
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
            var netid = "devnet";
            await SetupWallets(netid);
            if(netid != "xtest")
                await SetupEventsListener();

            #region prepare for trade, generate items
            // all trades happened in a DAO
            var daoName = "First DAO";
            var daoDesc = "Doing great business!";

            var dao = await CreateDaoAsync(genesisWallet, daoName, daoDesc);
            
            // dealer is necessary.
            await TestDealerAsync();

            // create some fiat for test
            var fiatUSD = await CreateTokenAsync(genesisWallet, "fiat", "USD", "US Dollar", 0);
            Assert.IsNotNull(fiatUSD, $"Can't print fiat");
            var fiatCNY = await CreateTokenAsync(genesisWallet, "fiat", "CNY", "Chinese Yuan", 0);
            Assert.IsNotNull(fiatCNY, $"Can't print fiat");
            var fiatGBP = await CreateTokenAsync(genesisWallet, "fiat", "GBP", "British Pound", 0);
            Assert.IsNotNull(fiatGBP, $"Can't print fiat");
            // tmp zone for fast test

            // end
            
            // try to sell LYR for fiat/USD
            Console.WriteLine("try to sell LYR for fiat/USD");
            _currentTestTask = "LYR2Tether";
            var lyrgenret = await testWallet.RPC.GetTokenGenesisBlockAsync(testWallet.AccountId, "LYR", "");
            var lyrgen = lyrgenret.As<LyraTokenGenesisBlock>();
            await TestUniTradeAsync(dao, testWallet, lyrgen, test2Wallet, fiatUSD);

            // try to sell fiat/USD for LYR
            Console.WriteLine("try to sell fiat/USD for LYR");
            _currentTestTask = "Tether2LYR";
            await TestUniTradeAsync(dao, testWallet, fiatUSD, test2Wallet, lyrgen);

            

            // tot
            var metaurl1 = await CreateTotMetaDataAsync(netid, testWallet, HoldTypes.TOT, "tot1", "test tot 1", null);
            var totg1 = await CreateTestToTAsync(netid, testWallet, HoldTypes.TOT, metaurl1);

            var metaurl2 = await CreateTotMetaDataAsync(netid, test2Wallet, HoldTypes.TOT, "tot2", "test tot 2", null);
            var totg2 = await CreateTestToTAsync(netid, test2Wallet, HoldTypes.TOT, metaurl2);

            //var fiatmetaurl1 = await CreateTotMetaDataAsync(netid, testWallet, HoldTypes.Fiat, "fiat1, USD", "test fiat 1", null);
            //var fiatg = await CreateTestToTAsync(netid, testWallet, HoldTypes.Fiat, fiatmetaurl1);

            //var fiatmetaurl2 = await CreateTotMetaDataAsync(netid, test2Wallet, HoldTypes.Fiat, "fiat2, CNY", "test fiat 2", null);
            //var fiatg2 = await CreateTestToTAsync(netid, test2Wallet, HoldTypes.Fiat, fiatmetaurl2);

            //var fiatg = await CreateTokenAsync(genesisWallet, "fiat", "USD", "US Dollar", 0);
            //var fiatg2 = await CreateTokenAsync(genesisWallet, "fiat", "CNY", "China Yuan", 0);
            var tetherg = await CreateTokenAsync(genesisWallet, "tether", "USDT", "USDT", 10000000);

            Assert.IsTrue((await genesisWallet.SendAsync(10000m, testPublicKey, tetherg.Ticker)).Successful());
            Assert.IsTrue((await genesisWallet.SendAsync(10000m, test2PublicKey, tetherg.Ticker)).Successful());



            // create and sell NFT
            var nftg1 = await CreateTestNFTAsync(testWallet);
            Assert.IsNotNull(nftg1);

            var nftg2 = await CreateTestNFTAsync(testWallet);
            Assert.IsNotNull(nftg2);
            #endregion
            var t = CreateTestNFTAsync(test2Wallet);


            //Console.WriteLine("Test fiat to tot");
            //_currentTestTask = "Fiat2TOT";
            //await TestUniTradeAsync(dao, testWallet, fiatg, test2Wallet, totg2);

            Console.WriteLine("Test sell nft to test2 for tether usd");
            _currentTestTask = "NFT2Tether";
            await TestUniTradeAsync(dao, testWallet, nftg2, test2Wallet, tetherg);

            //Console.WriteLine("Test sell nft OTC to test2 for fiat");
            //_currentTestTask = "NFT2Fiat";
            //await TestUniTradeAsync(dao, testWallet, nftg1, test2Wallet, fiatg);

            //Console.WriteLine("Test tot to fiat");
            //_currentTestTask = "TOT2Fiat";
            //await TestUniTradeAsync(dao, testWallet, totg1, test2Wallet, fiatg2);

            Console.WriteLine("Test tot to tot");
            _currentTestTask = "TOT2TOT";
            await TestUniTradeAsync(dao, testWallet, totg1, test2Wallet, totg2);

            await TestTradeMatrixAsync(netid, dao);

            _currentTestTask = "DAOCHG";
            //await TestChangeDAO();
            // after test, dump the database statistics

            _currentTestTask = "*";

            //var tradeid = await TestUniTradeDispute();   // test for dispute
            //await TestVoting(tradeid); // related to dealer. bypass. real test in Uni unit test

            await TestPoolAsync();
            await TestProfitingAndStaking();
            //await TestNodeFee();

            //// let workflow to finish
            //await Task.Delay(1000);
            if (netid == "xtest")
                Console.WriteLine(cs.PrintProfileInfo());
        }

        private async Task TestTradeMatrixAsync(string netid, IDao dao)
        {
            foreach (var tsell in Enum.GetValues(typeof(HoldTypes)).Cast<HoldTypes>())
                foreach (var tbuy in Enum.GetValues(typeof(HoldTypes)).Cast<HoldTypes>())
                {
                    Console.WriteLine($"\n----- Test sell {tsell} to test2 for {tbuy}\n");
                    await TestTradeForTypeAsync(netid, dao, tsell, tbuy);
                }
        }

        private async Task TestTradeForTypeAsync(string netid, IDao dao, HoldTypes tsell, HoldTypes tbuy)
        {
            var tgsell = await CreateForHoldTypeAsync(netid, tsell, testWallet, true);
            Assert.IsNotNull(tgsell, $"selling genesis for {tsell} is not ok");

            var tgbuy = await CreateForHoldTypeAsync(netid, tbuy, test2Wallet, false);
            Assert.IsNotNull(tgbuy, $"buying genesis for {tbuy} is not ok");
            
            _currentTestTask = $"{tsell}2{tbuy}";
            await TestUniTradeAsync(dao, testWallet, tgsell, test2Wallet, tgbuy);
        }

        /// <summary>
        /// fiat will use a combination of DEX and NFT. a virtual federal reserve will print fiat for user per request.
        /// </summary>
        /// <param name="netid"></param>
        /// <param name="holdtype"></param>
        /// <param name="wallet"></param>
        /// <returns></returns>
        private async Task<TokenGenesisBlock> CreateForHoldTypeAsync(string netid, HoldTypes holdtype, Wallet wallet, bool IsSell)
        {
            Random r = new Random();
            var tokenName = $"Hod-{holdtype}-{r.Next()}";
            var domainName = "unittest";
            var ticker = $"{domainName}/{tokenName}";
            switch (holdtype)
            {
                case HoldTypes.Token:
                    var result0 = await wallet.CreateTokenAsync(tokenName, domainName, "", 0, 50000000000, true, "", "", "", ContractTypes.Cryptocurrency, null);
                    Assert.IsTrue(result0.Successful());
                    await wallet.SyncAsync();
                    var result1 = await wallet.RPC.GetTokenGenesisBlockAsync(wallet.AccountId, ticker, "");
                    Assert.IsTrue(result1.Successful());
                    var tokenGen = result1.As<TokenGenesisBlock>();
                    Assert.IsNotNull(tokenGen, $"Can't get token gensis for {ticker}");
                    return tokenGen;
                case HoldTypes.NFT:
                    var nftg1 = await CreateTestNFTAsync(wallet);
                    Assert.IsNotNull(nftg1);
                    return nftg1;
                case HoldTypes.Fiat:
                    if(IsSell)
                    {
                        var fgs = await wallet.RPC.GetTokenGenesisBlockAsync(wallet.AccountId, "fiat/USD", "");
                        Assert.IsTrue(fgs.Successful());
                        return fgs.As<TokenGenesisBlock>();
                    }
                    else
                    {
                        var fgs = await wallet.RPC.GetTokenGenesisBlockAsync(wallet.AccountId, "fiat/CNY", "");
                        Assert.IsTrue(fgs.Successful());
                        return fgs.As<TokenGenesisBlock>();
                    }
                case HoldTypes.TOT:
                case HoldTypes.SVC:
                    var metaurl1 = await CreateTotMetaDataAsync(netid, testWallet, HoldTypes.TOT, tokenName, "test tot 1", null);
                    var totg1 = await CreateTestToTAsync(netid, wallet, holdtype, metaurl1);
                    return totg1;
                default:
                    Assert.Inconclusive($"Hold type {holdtype} is not supported yet.");
                    break;
            }
            return null;
        }

        /// <summary>
        /// the owner request to print fiat. WF will print it.
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="symbol"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private async Task PrintFiatAsync(Wallet owner, string symbol, long count)
        {
            var existsWalletRet = await owner.RPC.FindFiatWalletAsync(owner.AccountId, symbol);
            if (!existsWalletRet.Successful())
            {
                var crwlt = new Wallet.LyraContractABI
                {
                    svcReq = BrokerActions.BRK_FIAT_CRACT,
                    targetAccountId = LyraGlobal.GUILDACCOUNTID,
                    amounts = new Dictionary<string, decimal>
                    {
                        { LyraGlobal.OFFICIALTICKERCODE, 1 },
                    },
                    objArgument = new FiatCreateWallet
                    {
                        symbol = symbol,
                    }
                };

                var result = await owner.ServiceRequestAsync(crwlt);
                Assert.IsTrue(result.Successful(), $"Can't create fiat wallet: {result.ResultCode}");
                await WaitWorkflow(result.TxHash, "Create Fiat Wallet");
            }

            // then we should get the wallet
            var fwquery = await owner.RPC.GetAllFiatWalletsAsync(owner.AccountId);
            Assert.IsTrue(fwquery.Successful(), $"Can't get fiat wallet just created");
            var blks = fwquery.GetBlocks();
            Assert.IsTrue(blks.Count() > 0, "fiat wallet not found.");

            var printMoeny = new Wallet.LyraContractABI
            {
                svcReq = BrokerActions.BRK_FIAT_PRINT,
                targetAccountId = LyraGlobal.GUILDACCOUNTID,
                amounts = new Dictionary<string, decimal>
                    {
                        { LyraGlobal.OFFICIALTICKERCODE, 1 },
                    },
                objArgument = new FiatPrintMoney
                {
                    symbol = symbol,
                    amount = count,
                }
            };

            var result2 = await owner.ServiceRequestAsync(printMoeny);
            Assert.IsTrue(result2.Successful(), $"Can't create fiat wallet: {result2.ResultCode}");
            await WaitWorkflow(result2.TxHash, "Print Fiat into wallet");

            await owner.SyncAsync();
            var balances = owner.GetLastSyncBlock().Balances;
            Assert.IsTrue(balances.ContainsKey(symbol), $"No balance for {symbol}");
            Assert.IsTrue(balances[symbol].ToBalanceDecimal() >= count, $"Fiat print failed. balance of {symbol} not right: {balances[symbol].ToBalanceDecimal()}");
        }

        private async Task<TokenGenesisBlock> CreateTokenAsync(Wallet ownerWallet, string domain, string token, 
            string tokenDesc, decimal supply)
        {
            var find0 = await ownerWallet.GetTokenGenesisBlockAsync(domain + "/" + token);
            if(find0 != null)
            {
                return find0;
            }
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
            var daochkret = await testWallet.RPC.GetDaoByNameAsync(daoName);
            if (!daochkret.Successful())
            {
                var dcret = await ownerWallet.CreateDAOAsync(daoName, daoDesc, 1, 0.01m, 0.001m, 10, 120, 130);
                Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");
                await WaitWorkflow(dcret.TxHash, "CreateDAOAsync");
            }

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
            if(ownerWallet.NetworkId == "xtest")
                Assert.AreEqual(2, daos.Count(), $"can't find dao by GetAllDaosAsync");
            var dao0 = alldaoret.GetBlocks().First() as DaoGenesisBlock;
            //Assert.IsTrue(daoblk.AuthCompare(dao0));

            // get dao by the IBroker api
            var brkblksret = await ownerWallet.RPC.GetAllBrokerAccountsForOwnerAsync(ownerWallet.AccountId);
            Assert.IsTrue(brkblksret.Successful(), $"Can't get DAO by brk api: {brkblksret.ResultCode}");
            var daoblk2 = brkblksret.GetBlocks().FirstOrDefault(a => a is DaoGenesisBlock) as DaoGenesisBlock;
            if (ownerWallet.NetworkId == "xtest")
            {
                Assert.AreEqual(daoName, daoblk2.Name);
                Assert.AreEqual(daoDesc, daoblk2.Description);
            }

            var dao1 = daoret.GetBlock() as IDao;
            return dao1;
        }

        private async Task TestChangeDAO()
        {
            // create a DAO for nodes
            var name = "Node Owners Club x";
            var desc = "Doing great business!";

            var daoq = await genesisWallet.RPC.GetDaoByNameAsync(name);

            if (!daoq.Successful())
            {
                var dcret = await genesisWallet.CreateDAOAsync(name, desc, 1, 0.01m, 0.005m, 10, 120, 120);
                Assert.IsTrue(dcret.Successful(), $"failed to create DAO: {dcret.ResultCode}");

                await WaitWorkflow(dcret.TxHash, "CreateDAOAsync");
            }

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

            await WaitWorkflow(chgret.TxHash, "Change DAO");
            Assert.IsTrue(_authResult);

            await testWallet.SyncAsync();
            var cp0 = testWallet.BaseBalance;

            // test non-owner
            var chgx21 = await testWallet.ChangeDAO(nodesdao.AccountID, null, change);
            Assert.IsTrue(chgx21.Successful(), $"Should not error change DAO 21: {chgx21.ResultCode}");
            await WaitWorkflow(chgx21.TxHash, "Change DAO Wrong 21", APIResultCodes.Unauthorized);

            await testWallet.SyncAsync();
            var cp1 = testWallet.BaseBalance;
            Assert.AreEqual(cp0 - 1, cp1, $"The refund is not OK! diff {cp0 - cp1}");

            // wrong creator
            var chgx2 = await genesisWallet.ChangeDAO(nodesdao.AccountID, null, change.With(
                new
                {
                    creator = testWallet.AccountId,
                }
                ));
            Assert.IsTrue(chgx2.Successful(), $"Should not error change DAO 2: {chgx2.ResultCode}");
            await WaitWorkflow(chgx2.TxHash, "Change DAO Wrong 2", APIResultCodes.Unauthorized);

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
            Assert.IsTrue(chgx22.Successful(), $"Should not error change DAO 22: {chgx22.ResultCode}");
            await WaitWorkflow(chgx22.TxHash, "Change DAO Wrong 22", APIResultCodes.ArgumentOutOfRange);

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
            Assert.IsTrue(chgx23.Successful(), $"Should not error change DAO 23: {chgx23.ResultCode}");
            await WaitWorkflow(chgx23.TxHash, "Change DAO Wrong 23", APIResultCodes.InvalidArgument);

            await testWallet.SyncAsync();
            var cp2 = testWallet.BaseBalance;
            Assert.AreEqual(cp0 - 1, cp2, $"The refund is not OK! diff {cp0 - cp2}");

            // test out of range settings
            change.settings["ShareRito"] = "1.2";
            change.settings["Description"] = null;
            var chgx1 = await genesisWallet.ChangeDAO(nodesdao.AccountID, null, change);
            Assert.IsTrue(chgx1.Successful(), $"Should not error change DAO: {chgx1.ResultCode}");
            await WaitWorkflow(chgx1.TxHash, "Change DAO Wrong 1", APIResultCodes.InvalidShareRitio);

            await testWallet.SyncAsync();
            var cp3 = testWallet.BaseBalance;
            Assert.AreEqual(cp0 - 1, cp3, $"The refund is not OK! diff {cp0 - cp3}");


            await TestJoinDAO(daoid);

            var rand = new Random();
            // test dao change by vote
            VotingSubject daochg = new VotingSubject
            {
                Type = SubjectType.DAOModify,
                DaoId = nodesdao.AccountID,
                Issuer = genesisWallet.AccountId,
                TimeSpan = 100,
                Title = $"We need to modify DAO ({rand.Next()})",
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
            await WaitWorkflow(daoVoteCrtRet.TxHash, "Create Vote for dao change Async");
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
            await WaitWorkflow(chgret2.TxHash, "Change DAO 2 by vote");

            // test api
            var execret = await genesisWallet.RPC.FindExecForVoteAsync(blockdv.AccountID);
            Assert.IsTrue(execret.Successful(), $"FindExecForVoteAsync ret {execret.ResultCode}");
            Assert.AreEqual(BlockTypes.OrgnizationChange, execret.GetBlock().BlockType);

            // test if dup exec detected
            var chgret3 = await genesisWallet.ChangeDAO(nodesdao.AccountID, blockdv.AccountID, change2);
            Assert.IsTrue(chgret3.Successful(), $"Should not Can't change DAO: {chgret3.ResultCode}");
            await WaitWorkflow(chgret3.TxHash, "Change DAO 3 by vote", APIResultCodes.AlreadyExecuted);

            // inconsist changes
            var chgret31 = await genesisWallet.ChangeDAO(nodesdao.AccountID, blockdv.AccountID, change2
                .With(
                    new
                    {
                        settings = new Dictionary<string, string>()
                    }
                ));
            Assert.IsTrue(chgret31.Successful(), $"should not Can't change DAO 31: {chgret31.ResultCode}");
            await WaitWorkflow(chgret31.TxHash, "Change DAO 31 by vote", APIResultCodes.ArgumentOutOfRange);
        }

        private async Task TestJoinDAO(string daoid)
        {
            if (testWallet.BaseBalance < 810000)
            {
                await genesisWallet.SendAsync(2000000, testWallet.AccountId);
                await testWallet.SyncAsync();
            }

            if (test2Wallet.BaseBalance < 810000)
            {
                await genesisWallet.SendAsync(2000000, test2Wallet.AccountId);
                await test2Wallet.SyncAsync();
            }

            if (test3Wallet.BaseBalance < 810000)
            {
                await genesisWallet.SendAsync(2000000, test3Wallet.AccountId);
                await test3Wallet.SyncAsync();
            }

            if (test4Wallet.BaseBalance < 810000)
            {
                await genesisWallet.SendAsync(2000000, test4Wallet.AccountId);
                await test4Wallet.SyncAsync();
            }

            // leave first.
            var daolastret = await client.GetLastBlockAsync(daoid);
            Assert.IsTrue(daolastret.Successful());
            var daolast = daolastret.As<IDao>();

            if(daolast.Treasure.ContainsKey(testPublicKey))
            {
                var leaveret1 = await testWallet.LeaveDAOAsync(daoid);
                Assert.IsTrue(leaveret1.Successful(), $"Can't leave DAO: {leaveret1.ResultCode}");
                await WaitWorkflow(leaveret1.TxHash, "LeaveDAOAsync 1");
                await testWallet.SyncAsync();
            }

            if (daolast.Treasure.ContainsKey(test2PublicKey))
            {
                var leaveret2 = await test2Wallet.LeaveDAOAsync(daoid);
                Assert.IsTrue(leaveret2.Successful(), $"Can't leave DAO: {leaveret2.ResultCode}");
                await WaitWorkflow(leaveret2.TxHash, "LeaveDAOAsync 2");
            }

            if (daolast.Treasure.ContainsKey(test3PublicKey))
            {
                var leaveret3 = await test3Wallet.LeaveDAOAsync(daoid);
                Assert.IsTrue(leaveret3.Successful(), $"Can't leave DAO: {leaveret3.ResultCode}");
                await WaitWorkflow(leaveret3.TxHash, "LeaveDAOAsync 3");
            }

            if (daolast.Treasure.ContainsKey(test4PublicKey))
            {
                var leaveret4 = await test4Wallet.LeaveDAOAsync(daoid);
                Assert.IsTrue(leaveret4.Successful(), $"Can't leave DAO: {leaveret4.ResultCode}");
                await WaitWorkflow(leaveret4.TxHash, "LeaveDAOAsync 4");
            }

            await test2Wallet.SyncAsync();
            await test3Wallet.SyncAsync();
            await test4Wallet.SyncAsync();

            // get dao
            var nodesdaoret = await genesisWallet.RPC.GetLastBlockAsync(daoid);
            Assert.IsTrue(nodesdaoret.Successful(), $"can't get dao: {nodesdaoret.ResultCode}");
            var nodesdao0 = nodesdaoret.GetBlock() as TransactionBlock;
            var name = (nodesdao0 as IDao).Name;
            var treasure0 = (nodesdao0 as IDao).Treasure.ToDecimalDict();

            // join DAO / invest
            var invret0 = await testWallet.JoinDAOAsync(daoid, 800m);
            Assert.IsTrue(invret0.Successful());
            await WaitWorkflow(invret0.TxHash, "JoinDAOAsync 0", APIResultCodes.InvalidAmount);

            if(testWallet.BaseBalance < 810000)
            {
                await genesisWallet.SendAsync(2000000, testWallet.AccountId);
                await testWallet.SyncAsync();
            }

            var invret = await testWallet.JoinDAOAsync(daoid, 800000m);
            Assert.IsTrue(invret.Successful());
            await WaitWorkflow(invret.TxHash, "JoinDAOAsync 1");

            nodesdaoret = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret.Successful());
            var nodesdao = nodesdaoret.GetBlock() as TransactionBlock;
            var treasure = (nodesdao as IDao).Treasure.ToDecimalDict();
            var startbalance1 = treasure0.ContainsKey(testPublicKey) ? treasure0[testPublicKey] : 0;
            Assert.AreEqual(startbalance1 + 800800m, Math.Round(treasure[testPublicKey], 5));

            // another join DAO
            var invret2 = await test2Wallet.JoinDAOAsync(daoid, 150000m);
            Assert.IsTrue(invret2.Successful());

            await WaitWorkflow(invret2.TxHash, "JoinDAOAsync 2");

            var invret3 = await test3Wallet.JoinDAOAsync(daoid, 50000m);
            Assert.IsTrue(invret3.Successful());

            await WaitWorkflow(invret3.TxHash, "JoinDAOAsync 3");

            var invret4 = await test4Wallet.JoinDAOAsync(daoid, 50000m);
            Assert.IsTrue(invret4.Successful());

            await WaitWorkflow(invret4.TxHash, "JoinDAOAsync 4");

            // then we expect the treasure rito
            nodesdaoret = await genesisWallet.RPC.GetDaoByNameAsync(name);
            Assert.IsTrue(nodesdaoret.Successful());
            nodesdao = nodesdaoret.GetBlock() as TransactionBlock;
            treasure = (nodesdao as IDao).Treasure.ToDecimalDict();
            Assert.AreEqual(startbalance1 + 800800m, Math.Round(treasure[testPublicKey], 5));
            Assert.AreEqual((treasure0.ContainsKey(test2PublicKey) ? treasure0[test2PublicKey] : 0) + 150000m, Math.Round(treasure[test2PublicKey], 5));
            Assert.AreEqual((treasure0.ContainsKey(test3PublicKey) ? treasure0[test3PublicKey] : 0) + 50000m, Math.Round(treasure[test3PublicKey], 5));
            Assert.AreEqual((treasure0.ContainsKey(test4PublicKey) ? treasure0[test4PublicKey] : 0) + 50000m, Math.Round(treasure[test4PublicKey], 5));

            // test leave DAO
            var leaveret4x = await test4Wallet.LeaveDAOAsync(daoid);
            Assert.IsTrue(leaveret4x.Successful(), $"Can't leave DAO: {leaveret4x.ResultCode}");
            await WaitWorkflow(leaveret4x.TxHash, "LeaveDAOAsync 4");

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

            await WaitWorkflow(voteCrtRet.TxHash, "Create Vote Subject Async");
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
            await WaitWorkflow(invret4.TxHash, "join after vote genesis");

            var voteRet41 = await test4Wallet.Vote((curvote as TransactionBlock).AccountID, 1);
            Assert.IsTrue(voteRet41.ResultCode == APIResultCodes.Unauthorized, $"Vote 41 should error: {voteRet41.ResultCode}");
            await WaitBlock("Vote on Subject Async 41");

            // clean
            var leaveret4 = await test4Wallet.LeaveDAOAsync(trade.Trade.daoId);
            Assert.IsTrue(leaveret4.Successful(), $"Can't leave DAO: {leaveret4.ResultCode}");
            await WaitWorkflow(leaveret4.TxHash, "clean join after vote genesis");

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

            await WaitWorkflow(odrRet.TxHash, "ExecuteResolution");

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

        private async Task DoVote(string voteSendHash, bool success)
        {
            var voteblksRet = await genesisWallet.RPC.GetBlocksByRelatedTxAsync(voteSendHash);
            Assert.IsTrue(voteblksRet.Successful());
            var relblocks = voteblksRet.GetBlocks<TransactionBlock>().ToList();
            Assert.IsTrue(relblocks.Count > 0);
            var firstmgblk = relblocks[0];
            Assert.IsTrue(firstmgblk.Tags?.ContainsKey(Block.MANAGEDTAG));
            var mgmttag = firstmgblk.Tags[Block.MANAGEDTAG];
            Enum.TryParse(mgmttag, out WFState wfstate);
            Assert.AreNotEqual(WFState.Refund, wfstate);

            var voteblk = relblocks.Where(a => a is VotingGenesisBlock)
                .FirstOrDefault();
            Assert.IsNotNull(voteblk);

            var voteRet = await testWallet.Vote(voteblk.AccountID, 0);
            Assert.IsTrue(voteRet.Successful(), $"Vote error: {voteRet.ResultCode}");
            await WaitWorkflow(voteRet.TxHash, "Vote on Subject Async");

            var voteRet2 = await test2Wallet.Vote(voteblk.AccountID, 1);
            Assert.IsTrue(voteRet2.Successful(), $"Vote error: {voteRet2.ResultCode}");
            await WaitWorkflow(voteRet2.TxHash, "Vote on Subject Async 2");

            // vote again to trigger an error
            var voteRet2x = await test2Wallet.Vote(voteblk.AccountID, 0);
            Assert.IsTrue(voteRet2x.Successful(), $"Vote 2x should not error: {voteRet2x.ResultCode}");
            await WaitWorkflow(voteRet2x.TxHash, "Vote on Subject Async 2x", APIResultCodes.InvalidVote);

            if (success)
            {
                var voteRet3 = await test3Wallet.Vote(voteblk.AccountID, 0);
                await WaitWorkflow(voteRet3.TxHash, "Vote on Subject Async 3");
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

            // if fiat, we need to request print some
            if(offeringGen.Ticker.StartsWith("fiat/"))
            {
                await PrintFiatAsync(offeringWallet, offeringGen.Ticker, 1000000);
            }
            if (bidingGen.Ticker.StartsWith("fiat/"))
            {
                await PrintFiatAsync(bidingWallet, bidingGen.Ticker, 1000000);
            }

            // default is NFT2Tether
            var offeringCost = -1;
            var bidingCost = 3;
            var daoCost = 2;
            switch (_currentTestTask)
            {
                case "NFT2Fiat":
                    offeringCost = 3;
                    daoCost = 1;
                    break;
                case "TOT2Fiat":
                    offeringCost = 5;
                    daoCost = 1;
                    break;
                case "TOT2TOT":
                    offeringCost = 5;
                    bidingCost = 5;
                    daoCost = 1;
                    break;
                case "Fiat2TOT":
                    offeringCost = 5;
                    daoCost = 1;
                    break;
                default: break;
            }

            decimal eqprice = 100m;
            decimal amount = 3m;
            var totalOrderValue = amount * eqprice;
            (var tradeFee, var networkFee) = UniTradeFees.CalculateSellerFees(eqprice, amount, (dao as IDao).SellerFeeRatio);
            var totalFee = tradeFee + networkFee;
            var totalCollateral = totalOrderValue * ((dao as IDao).SellerPar / 100m) + totalFee;

            // calculate fees
            await offeringWallet.SyncAsync();            
            var offeringBalanceInput = offeringWallet.BaseBalance;
            await bidingWallet.SyncAsync();
            var bidingBalanceInput = bidingWallet.BaseBalance;
            // after trading, the balance should be

            var offeringBalanceShouldBe = offeringBalanceInput
                - offeringCost       // sending fee
                - LyraGlobal.GetListingFeeFor()
                - totalCollateral;
            var bidingBalanceShouldBe = bidingBalanceInput
                - bidingCost         // sending fee
                - totalCollateral;
            long offeringBalanceTokenInputLong;
            offeringWallet.GetLastSyncBlock().Balances.TryGetValue(offeringGen.Ticker, out offeringBalanceTokenInputLong);
            var offeringBalanceTokenInput = offeringBalanceTokenInputLong.ToBalanceDecimal();
            var bidingBalanceTokenInput = bidingWallet.GetLastSyncBlock().Balances.ContainsKey(offeringGen.Ticker) ?
                bidingWallet.GetLastSyncBlock().Balances[offeringGen.Ticker].ToBalanceDecimal() : 0;

            var daolatest = (await offeringWallet.RPC.GetLastBlockAsync(dao.AccountID)).As<TransactionBlock>();
            var daoBalanceInput = daolatest.Balances.ContainsKey("LYR") ? daolatest.Balances["LYR"].ToBalanceDecimal() : 0;
            var daoBalanceShouldBe = daoBalanceInput
                + LyraGlobal.GetListingFeeFor()
                + totalFee
                - daoCost         // a send
                ;

            if(!offeringGen.Ticker.StartsWith("fiat/"))
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
                price = 2,
                eqprice = eqprice,
                cltamt = totalCollateral,
                payBy = new string[] { "Paypal" },

                amount = amount,
                limitMin = 1,
                limitMax = 3,
            };

            var ret = await offeringWallet.CreateUniOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"{_currentTestTask} Can't create order: {ret.ResultCode} for {offeringWallet.AccountId}");

            //await Task.Delay(5000);
            //return;
            await WaitWorkflow(ret.TxHash, $"CreateUniOrderAsync");

            var daoBalance = daoBalanceInput + 100; // listing fee to the DAO
            await DaoTraeasureShouldBe(dao, daoBalance);

            var Uniret = await offeringWallet.RPC.GetUniOrdersByOwnerAsync(offeringWallet.AccountId);
            Assert.IsTrue(Uniret.Successful(), $"Can't get Uni gensis block. {Uniret.ResultCode}");
            var uniOrders = Uniret.GetBlocks();
            Assert.IsTrue(uniOrders.First() is IUniOrder, $"Uni order gensis block not found.");

            // test find tradable orders
            var tradableret = await offeringWallet.RPC.FindTradableUniAsync();
            Assert.IsTrue(tradableret.Successful(), "Unable to find tradable.");
            var tradableblks = tradableret.GetBlocks("orders");
            if(offeringWallet.NetworkId == "xtest")
                Assert.AreEqual(1, tradableblks.Count(), $"Trade tradable block count is {tradableblks.Count()}");
            var firsttradable = tradableblks.First();
            Assert.IsTrue(firsttradable is IUniOrder fodr && fodr.Name == "no name");

            var curOdrId = (firsttradable as IUniOrder).AccountID;
            Console.WriteLine($"\n\nOrder ID is {curOdrId}\n\n");

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

            // result is time reverse sorted
            var curUniOrder = uniOrders.First(a => a.BlockType == BlockTypes.UniOrderGenesis) as UniOrderGenesisBlock;
            Assert.IsTrue(order.Equals(curUniOrder.Order), "Uni order not equal.");
            Assert.AreEqual(curOdrId, curUniOrder.AccountID);

            await PrintBalancesForAsync(offeringWallet.AccountId, bidingWallet.AccountId,
                dao.AccountID, curUniOrder.AccountID);

            await test2Wallet.SyncAsync(null);
            var test2balance = test2Wallet.BaseBalance;
            var tradgen = await CreateUniTradeAsync(dao, testWallet, test2Wallet, curUniOrder);

            bool hasOTC = false;
            if (LyraGlobal.GetOTCRequirementFromTicker(offeringGen.Ticker))
            {
                Console.WriteLine($"{_currentTestTask} Confirm OTC from seller");
                await ConfirmOTCAsync(tradgen, false, offeringWallet, bidingWallet);
                hasOTC = true;
            }
            if (LyraGlobal.GetOTCRequirementFromTicker(bidingGen.Ticker))
            {
                Console.WriteLine($"{_currentTestTask} Confirm OTC from buyer");
                await ConfirmOTCAsync(tradgen, true, bidingWallet, offeringWallet);
                hasOTC = true;
            }

            // trade is done.
            var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");
            Assert.AreEqual(UniTradeStatus.Closed, (trdlatest2.GetBlock() as IUniTrade).UTStatus,
                $"Trade status not changed to Closed");

            if(hasOTC)
            {
                // trade is ok. now its time to close the order
                var closeret2 = await offeringWallet.CloseUniOrderAsync(dao.AccountID, curUniOrder.AccountID);
                Assert.IsTrue(closeret2.Successful(), $"Unable to close order: {closeret2.ResultCode}");
                await WaitWorkflow(closeret2.TxHash, $"CloseUniOrderAsync");
            }

            var ordfnlret = await offeringWallet.RPC.GetLastBlockAsync(curUniOrder.AccountID);
            Assert.IsTrue(ordfnlret.Successful(), $"Can't get order latest block: {ordfnlret.ResultCode}");
            var odrfnlblk = ordfnlret.GetBlock() as IUniOrder;

            if(odrfnlblk.Order.amount == 0)
            {
                Assert.AreEqual(UniOrderStatus.Closed, odrfnlblk.UOStatus,
                    $"Order status not changed to Closed: {odrfnlblk.UOStatus}");
                Assert.AreEqual(0, (ordfnlret.GetBlock() as TransactionBlock).Balances["LYR"], "LYR not zero");
            }
            else
            {
                Assert.AreEqual(UniOrderStatus.Partial, odrfnlblk.UOStatus,
                    $"Order status not changed to Partial: {odrfnlblk.UOStatus}");
            }


            await test2Wallet.SyncAsync(null);

            // buyer fee calculated as LYR
            var totalAmount = tradgen.Trade.amount;
            //decimal totalFee = 0;
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
            Assert.AreEqual(curOdrId, odrfnlblk.AccountID);
            
            if (odrfnlblk.Order.amount > 0)
            {
                Console.WriteLine($"Delisting order: {curUniOrder.AccountID}");
                var dlret = await offeringWallet.DelistUniOrderAsync(dao.AccountID, curUniOrder.AccountID);
                Assert.IsTrue(dlret.Successful(), $"Unable to delist order: {dlret.ResultCode}");
                await WaitWorkflow(dlret.TxHash, $"DelistUniOrderAsync");

                var orddlret = await offeringWallet.RPC.GetLastBlockAsync(curUniOrder.AccountID);
                Assert.IsTrue(orddlret.Successful(), $"Can't get order latest block: {orddlret.ResultCode}");
                Assert.AreEqual(UniOrderStatus.Delist, (orddlret.GetBlock() as IUniOrder).UOStatus,
                    $"Order status not changed to Delisted");
            }

            if(odrfnlblk.UOStatus != UniOrderStatus.Closed)
            {
                //trade is ok.now its time to close the order
                var closeret = await offeringWallet.CloseUniOrderAsync(dao.AccountID, curUniOrder.AccountID);
                Assert.IsTrue(closeret.Successful(), $"Unable to close order: {closeret.ResultCode}");

                await WaitWorkflow(closeret.TxHash, $"CloseUniOrderAsync");
                var ordfnlret2 = await offeringWallet.RPC.GetLastBlockAsync(curUniOrder.AccountID);
                Assert.IsTrue(ordfnlret2.Successful(), $"Can't get order latest block: {ordfnlret2.ResultCode}");
                Assert.AreEqual(UniOrderStatus.Closed, (ordfnlret2.GetBlock() as IUniOrder).UOStatus,
                    $"Order status not changed to Closed: {(ordfnlret2.GetBlock() as IUniOrder).UOStatus}");
                Assert.AreEqual(0, (ordfnlret2.GetBlock() as TransactionBlock).Balances["LYR"], "LYR not zero");

                await offeringWallet.SyncAsync(null);
            }

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
                dao.AccountID, curUniOrder.AccountID, tradgen.AccountID);

            await Task.Delay(100);
            Assert.IsTrue(_authResult, $"Authorizer failed: {_sbAuthResults}");
            ResetAuthFail();

            await offeringWallet.SyncAsync();
            var offeringBalanceOut = offeringWallet.BaseBalance;
            // TODO: verify the balance
            //Assert.AreEqual(offeringBalanceShouldBe, offeringBalanceOut, $"{_currentTestTask} Offering wallet balance is not right, diff: {offeringBalanceOut - offeringBalanceShouldBe}");
            
            await bidingWallet.SyncAsync();
            var bidingBalanceOut = bidingWallet.BaseBalance;
            // tmp
            //Assert.AreEqual(bidingBalanceShouldBe, bidingBalanceOut, $"Biding for {offeringGen.Ticker} to {bidingGen.Ticker} wallet balance is not right, diff: {bidingBalanceOut - bidingBalanceShouldBe}");

            var offeringBalanceTokenOut = offeringWallet.GetLastSyncBlock().Balances[offeringGen.Ticker].ToBalanceDecimal();
            var bidingBalanceTokenOut = bidingWallet.GetLastSyncBlock().Balances.ContainsKey(offeringGen.Ticker) ?
                bidingWallet.GetLastSyncBlock().Balances[offeringGen.Ticker].ToBalanceDecimal() : 0;

            if(offeringGen.Ticker == "LYR")
            {
                var offShouldBe = offeringBalanceTokenInput - 1 - LyraGlobal.GetListingFeeFor();
                //Assert.AreEqual(offShouldBe, offeringBalanceTokenOut, $"delta: {offShouldBe- offeringBalanceTokenOut}, collateral: {collateralCount}");
                //Assert.AreEqual(bidingBalanceTokenInput + 1, bidingBalanceTokenOut);
            }
            else
            {
                Assert.AreEqual(offeringBalanceTokenInput - 1, offeringBalanceTokenOut);
                Assert.AreEqual(bidingBalanceTokenInput + 1, bidingBalanceTokenOut);
            }

            var daolatest2 = (await offeringWallet.RPC.GetLastBlockAsync(dao.AccountID)).As<TransactionBlock>();
            var daoBalanceOutput = daolatest2.Balances["LYR"].ToBalanceDecimal();
            //Assert.AreEqual(daoBalanceShouldBe, daoBalanceOutput, $"Dao treasure balance is not right, diff: {daoBalanceOutput - daoBalanceShouldBe} dao addr: {daolatest2.AccountID}");

            // test cancellation
            //var order2 = new UniOrder
            //{
            //    daoId = dao.AccountID,
            //    dealerId = dlr.AccountID,
            //    offerby = LyraGlobal.GetHoldTypeFromTicker(offeringGen.Ticker),
            //    offering = offeringGen.Ticker,
            //    bidby = LyraGlobal.GetHoldTypeFromTicker(bidingGen.Ticker),
            //    biding = bidingGen.Ticker,
            //    price = 2,
            //    cltamt = collateralCount,
            //    payBy = new string[] { "Paypal" },

            //    amount = 1,
            //    limitMin = 1,
            //    limitMax = 1,
            //};

            //var retx2 = await offeringWallet.CreateUniOrderAsync(order2);
            //var Uniret2 = await offeringWallet.RPC.GetUniOrdersByOwnerAsync(offeringWallet.AccountId);
            //Assert.IsTrue(Uniret.Successful(), $"Can't get Uni gensis block. {Uniret.ResultCode}");
            //var uniOrders2 = Uniret2.GetBlocks();

            //var tradgenC = await CreateUniTradeAsync(dao, testWallet, test2Wallet, uniOrders2.FirstOrDefault(a => a is UniOrderGenesisBlock) as UniOrderGenesisBlock, collateralCount);
            //var tradegenCGensRet = await test2Wallet.RPC.FindBlockByHeightAsync((tradgenC as TransactionBlock).AccountID, 1);
            //await CancelUniTrade(test2Wallet, tradegenCGensRet.As<UniTradeGenesisBlock>());
        }

        private async Task ConfirmOTCAsync(IUniTrade tradeLatest, bool IsBid, Wallet fromWallet, Wallet toWallet)
        {
            //await CancelUniTrade(test2Wallet, tradgen);
            //await test2Wallet.SyncAsync(null);
            //var test2balanceA = test2Wallet.BaseBalance;
            //Assert.AreEqual(test2balance - 3m, test2balanceA, "Balance not ok after cancel trade.");

            //tradgen = await CreateUniTradeAsync(dao, testWallet, test2Wallet, Unig, collateralCount);
            // cancel one
            var tradeQueryRet2 = await testWallet.RPC.GetLastBlockAsync(tradeLatest.AccountID);
            Assert.IsTrue(tradeQueryRet2.Successful(), $"Can't query trade via GetLastBlockAsync: {tradeQueryRet2.ResultCode}");
            var tradlast = tradeQueryRet2.GetBlock() as IUniTrade;
            Assert.IsTrue(tradlast.Delivery != null);
            //Assert.IsTrue(tradlast.Delivery.Proofs == null);

            // buyer send payment indicator
            var wlt = fromWallet;

            var pod1 = new ProofOfDilivery
            {
                Catalog = IsBid ? PoDCatalog.BidSent : PoDCatalog.OfferSent,
                Owner = wlt.AccountId,
                Carrier = tradeLatest.Trade.payVia,
                TrackingTag = "A000000000",
            };

            AuthorizationAPIResult payindret = await wlt.UniSendProofOfDiliveryAsync(tradeLatest.AccountID, pod1);
            Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

            await WaitWorkflow(payindret.TxHash, $"UniSendProofOfDiliveryAsync");
            // status changed to BuyerPaid
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradeLatest.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(UniTradeStatus.Processing, (trdlatest.GetBlock() as IUniTrade).UTStatus,
                $"Trade statust not changed to Delivering");

            tradlast = trdlatest.GetBlock() as IUniTrade;
            Assert.IsTrue(tradlast.Delivery.Proofs != null, "delivery proofs should not be null");
            Assert.IsTrue(tradlast.Delivery.Proofs.Count >= 1, "delivery proofs should have proof");
            Assert.IsTrue(tradlast.Delivery.Proofs.ContainsKey(pod1.Catalog), "delivery proofs should has proof of bid sent.");
            Assert.IsTrue(tradlast.Delivery.Proofs[pod1.Catalog] == pod1.Signature, "proof not right");

            // seller got the payment
            var wlt2 = toWallet;

            var pod2 = new ProofOfDilivery
            {
                Catalog = IsBid ? PoDCatalog.BidReceived : PoDCatalog.OfferReceived,
                Owner = wlt2.AccountId,
                Carrier = pod1.Carrier,
                TrackingTag = pod1.TrackingTag
            };

            var gotpayret = await wlt2.UniConfirmProofOfDiliveryAsync(tradeLatest.AccountID, pod2);
            Assert.IsTrue(gotpayret.Successful(), $"Got Payment indicator error: {payindret.ResultCode}");

            await WaitWorkflow(gotpayret.TxHash, $"UniConfirmProofOfDiliveryAsync");

            // status
            var trdlatest2 = await test2Wallet.RPC.GetLastBlockAsync(tradeLatest.AccountID);
            Assert.IsTrue(trdlatest2.Successful(), $"Can't get trade latest block: {trdlatest2.ResultCode}");

            tradlast = trdlatest2.GetBlock() as IUniTrade;
            Assert.IsTrue(tradlast.Delivery.Proofs != null, "delivery proofs should not be null");
            Assert.IsTrue(tradlast.Delivery.Proofs.Count >= 2, "delivery proofs should have proofs");
            Assert.IsTrue(tradlast.Delivery.Proofs.ContainsKey(pod2.Catalog), "delivery proofs should has proof of bid received.");
            Assert.IsTrue(tradlast.Delivery.Proofs[pod2.Catalog] == pod2.Signature, "proof not right");
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
                tradeStatusShouldBe = UniTradeStatus.Processing;
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
            await WaitWorkflow(cloret2.TxHash, "CancelUniTradeAsync 2", APIResultCodes.Success);

            ResetAuthFail();

            Assert.IsTrue(cloret.Successful(), $"Unable to cancel trade: {cloret.ResultCode}");

            // make sure the status of trade is Closed
            var latestret = await ownerWallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(latestret.Successful());
            var tradelst = latestret.GetBlock() as IUniTrade;
            Assert.AreEqual(UniTradeStatus.Canceled, tradelst.UTStatus, "not close trade properly");
        }

        private async Task<IUniTrade> CreateUniTradeAsync(IDao dao, Wallet offeringWallet, Wallet bidingWallet, UniOrderGenesisBlock Unig)
        {
            Console.WriteLine("Calling CreateUniTradeAsync");
            var dao1 = dao as TransactionBlock;
            // here comes a buyer, he who want to buy 1 BTC.
            var tradableret = await bidingWallet.RPC.FindTradableUniAsync();
            Assert.IsTrue(tradableret.Successful(), $"Can't find tradableorders: {tradableret.ResultCode}: {tradableret.ResultMessage}");
            var ords = tradableret.GetBlocks("orders");
            //if(offeringWallet.NetworkId == "xtest")
            //    Assert.AreEqual(1, ords.Count(), $"Order count not right: {ords.Count()}");
            //Assert.IsTrue((ords.First() as IUniOrder).Order.Equals(order), "Uni order not equal.");

            // get a snapshot of the order
            var odrblksnapshotret = await offeringWallet.RPC.GetLastBlockAsync(Unig.AccountID);
            var odrblksnapshot = odrblksnapshotret.As<IUniOrder>();
            Assert.IsTrue(odrblksnapshot.Order.amount >= 1, "amount avaliable in order show >= 1");

            decimal amount = 1;
            var totalTradeValue = Unig.Order.eqprice * amount;
            (var tradeFee, var networkFee) = UniTradeFees.CalculateBuyerFees(Unig.Order.eqprice, amount, (dao as IDao).BuyerFeeRatio);
            var totalFee = tradeFee + networkFee;
            var totalCollateral = totalTradeValue * ((dao as IDao).BuyerPar / 100m) + totalFee;

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
                price = Unig.Order.price,
                eqprice = Unig.Order.eqprice,
                
                cltamt = totalCollateral,
                payVia = "Paypal",
                amount = amount,
                pay = 2,
            };

            var traderet = await bidingWallet.CreateUniTradeAsync(trade);
            Assert.IsTrue(traderet.Successful(), $"Create Uni Trade error: {traderet.ResultCode}");
            Assert.IsFalse(string.IsNullOrWhiteSpace(traderet.TxHash), "No TxHash for trade create.");

            await WaitWorkflow(traderet.TxHash, $"CreateUniTradeAsync for sell");
            //await Task.Delay(1000); //maybe not needed
            //if(trade.biding.StartsWith("tether"))
            //{
            //    await Task.Delay(10000000);
            //    Assert.Fail();
            //}

            // the Uni order should now be amount 9
            var Uniret2 = await offeringWallet.RPC.GetUniOrdersByOwnerAsync(offeringWallet.AccountId);
            Assert.IsTrue(Uniret2.Successful(), $"Can't get Uni block. {Uniret2.ResultCode}");
            var Unis2 = Uniret2.GetBlocks();
            Assert.IsTrue(Unis2.First() is IUniOrder, $"Uni block count not = 1.");
            var Uniorderx = Unis2.First() as IUniOrder;
            
            Assert.AreEqual(odrblksnapshot.AccountID, Uniorderx.AccountID, $"got the wrong order block!!!");
            Assert.AreEqual(odrblksnapshot.Order.amount - 1, Uniorderx.Order.amount, $"Order {odrblksnapshot} height {(odrblksnapshot as Block).Height} amount {odrblksnapshot.Order.amount} remaining height {(Uniorderx as Block).Height} amount {Uniorderx.Order.amount} not right. state: {Uniorderx.UOStatus}");
            
            //if(direction == TradeDirection.Buy)
            //    Assert.IsTrue(0.9m == Uniorderx.Order.amount, "order not processed");
            //Assert.AreEqual(0.9m, Uniorderx.Order.amount, "order not processed");

            // get trade
            var related = await bidingWallet.RPC.GetBlocksByRelatedTxAsync(traderet.TxHash);
            Assert.IsTrue(related.Successful(), $"Can't get rleated tx for trade genesis: {related.ResultCode}");
            var blks = related.GetBlocks();
            var tradgenLast = blks.LastOrDefault() as IUniTrade;
            Assert.IsNotNull(tradgenLast, $"Can't get trade genesis: blks count: {blks.Count()}");
            Assert.AreEqual(trade, tradgenLast.Trade);

            // order's collateral should be
            var rito = trade.amount / ((IUniOrder)odrblksnapshot).Order.amount;
            var odrcltamtShouldBe = Math.Round(((IUniOrder)odrblksnapshot).Order.cltamt * rito, 8);
            Assert.AreEqual(odrcltamtShouldBe, tradgenLast.OdrCltMmt.ToBalanceDecimal(), $"OdrCltMmt in trade gen is not right!");
            // and with proper offering/biding token.
            
            if(tradgenLast.UTStatus == UniTradeStatus.Open)
            {
                Assert.IsTrue((tradgenLast as TransactionBlock).Balances[Unig.Order.offering] >= 1, $"offering not right.");
                Assert.IsTrue((tradgenLast as TransactionBlock).Balances[Unig.Order.biding] >= 2000, $"biding not right.");
            }
            else if(tradgenLast.UTStatus == UniTradeStatus.Closed)
            {
                //Assert.IsTrue((tradgenLast as TransactionBlock).Balances[Unig.Order.offering] >= 1, $"offering not right.");
                //Assert.IsTrue((tradgenLast as TransactionBlock).Balances[Unig.Order.biding] >= 2000, $"biding not right.");
            }


            var tradeStatusShouldBe = UniTradeStatus.Open;
            bool IsBidToken = !LyraGlobal.GetOTCRequirementFromTicker(trade.biding);
            bool IsOfferToken = !LyraGlobal.GetOTCRequirementFromTicker(trade.offering);
            if (IsBidToken && IsOfferToken)
            {
                tradeStatusShouldBe = UniTradeStatus.Closed;
            }

            // verify by api
            var tradeQueryRet = await bidingWallet.RPC.FindUniTradeAsync(bidingWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet.Successful(), $"Can't query trade via FindUniTradeAsync: {tradeQueryRet.ResultCode}");
            var tradeQueryResultBlocks = tradeQueryRet.GetBlocks();
            Assert.IsTrue(tradeQueryResultBlocks.Count() >= 1);
            Assert.AreEqual(tradgenLast.AccountID, (tradeQueryResultBlocks
                .OrderBy(a => a.TimeStamp)
                .Last() as TransactionBlock).AccountID);

            var tradeQueryRet2 = await offeringWallet.RPC.FindUniTradeAsync(offeringWallet.AccountId, false, 0, 10);
            Assert.IsTrue(tradeQueryRet2.Successful(), $"Can't query trade via FindUniTradeAsync: {tradeQueryRet2.ResultCode}");
            var tradeQueryResultBlocks2 = tradeQueryRet2.GetBlocks();
            //Assert.AreEqual(1, tradeQueryResultBlocks2.Count());
            Assert.AreEqual(tradgenLast.AccountID, (tradeQueryResultBlocks2
                .OrderBy(a => a.TimeStamp)
                .Last() as TransactionBlock).AccountID);

            var tradeQueryRet3 = await offeringWallet.RPC.FindUniTradeByStatusAsync(dao1.AccountID, tradeStatusShouldBe, 0, 10);
            Assert.IsTrue(tradeQueryRet3.Successful(), $"Can't query trade via FindUniTradeByStatusAsync: {tradeQueryRet3.ResultCode}");
            var tradeQueryResultBlocks3 = tradeQueryRet3.GetBlocks();
            //Assert.AreEqual(1, tradeQueryResultBlocks3.Count());
            Assert.AreEqual(tradgenLast.AccountID, (tradeQueryResultBlocks3.Last() as TransactionBlock).AccountID, $"In {_currentTestTask} create trade");

            return tradgenLast;
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

            await WaitWorkflow(dcret.TxHash, "CreateDAOAsync");

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
                //eqprice = 0.01m,
                //amount = 2,
                //collateral = 180000000,
                //collateralPrice = prices["LYR"],
                payBy = new string[] { "Paypal" },
                limitMin = 200,
                limitMax = 1000,
            };

            var ret = await testWallet.CreateUniOrderAsync(order);
            Assert.IsTrue(ret.Successful(), $"Can't create order: {ret.ResultCode}");

            await WaitWorkflow(ret.TxHash, $"CreateUniOrderAsync dispute sell");

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

            await WaitWorkflow(traderet.TxHash, "CreateUniTradeAsync");
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
            var tradlast = tradeQueryResultBlocks2.Last() as IUniTrade;
            Assert.IsTrue(tradlast.Delivery != null);
            Assert.IsTrue(tradlast.Delivery.Proofs.Count == 0);

            // buyer send payment indicator
            var payindret = await test2Wallet.UniSendProofOfDiliveryAsync(tradgen.AccountID, null);
            Assert.IsTrue(payindret.Successful(), $"Pay sent indicator error: {payindret.ResultCode}");

            await WaitWorkflow(payindret.TxHash, "UniTradeBuyerPaymentSentAsync");
            // status changed to BuyerPaid
            var trdlatest = await test2Wallet.RPC.GetLastBlockAsync(tradgen.AccountID);
            Assert.IsTrue(trdlatest.Successful(), $"Can't get trade latest block: {trdlatest.ResultCode}");
            Assert.AreEqual(UniTradeStatus.Processing, (trdlatest.GetBlock() as IUniTrade).UTStatus,
                $"Trade status not changed to BuyerPaid");

            tradlast = trdlatest.GetBlock() as IUniTrade;
            Assert.IsTrue(tradlast.Delivery != null);
            Assert.IsTrue(tradlast.Delivery.Proofs.Count == 1);
            Assert.IsTrue(tradlast.Delivery.Proofs.ContainsKey(PoDCatalog.BidSent));
            //Assert.IsTrue(Signatures.VerifyAccountSignature( tradlast.Delivery.Proofs[PoDCatalog.BidSent]));

            // seller not got the payment. seller raise a dispute
            var crdptret = await testWallet.UniTradeRaiseDisputeAsync(tradgen.AccountID);
            Assert.IsTrue(crdptret.Successful(), $"Raise dispute failed: {crdptret.ResultCode}");

            await WaitWorkflow(crdptret.TxHash, "UniTradeRaiseDisputeAsync");

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
            await WaitWorkflow(crpftret.TxHash, "create profiting");
            Assert.IsTrue(crpftret.Successful());
            var pfts = await genesisWallet.GetBrokerAccountsAsync<ProfitingGenesis>();
            var pftblock = pfts.FirstOrDefault();
            Assert.IsTrue(pftblock.OwnerAccountId == genesisWallet.AccountId);

            Console.WriteLine("Generate dividends");
            var crdvd = await genesisWallet.CreateDividendsAsync(pftblock.AccountID);
            await WaitWorkflow(crdvd.TxHash, "create dividents");
        }

        private async Task<IStaking> CreateStaking(Wallet w, string pftid, decimal amount)
        {
            ResetAuthFail();

            var crstkret = await w.CreateStakingAccountAsync($"moneybag{_rand.Next()}", pftid, 30, true);
            Assert.IsTrue(crstkret.Successful());
            await WaitWorkflow(crstkret.TxHash, "create staking");
            var stks = await w.GetBrokerAccountsAsync<StakingGenesis>();
            var stkblock = stks.LastOrDefault();
            Assert.IsTrue(stkblock.OwnerAccountId == w.AccountId);

            var addstkret = await w.AddStakingAsync(stkblock.AccountID, amount);
            Assert.IsTrue(addstkret.Successful());
            await WaitWorkflow(addstkret.TxHash, $"AddStakingAsync {addstkret.TxHash}");

            var stk = await w.GetStakingAsync(stkblock.AccountID);
            Assert.AreEqual(amount, (stk as TransactionBlock).Balances["LYR"].ToBalanceDecimal());
            return stk;
        }

        private async Task UnStaking(Wallet w, string stkid)
        {
            var balance = w.BaseBalance;
            var unstkret = await w.UnStakingAsync(stkid);
            Assert.IsTrue(unstkret.Successful(), $"Failed to UnStaking: {unstkret.ResultCode}");
            await WaitWorkflow(unstkret.TxHash, $"UnStakingAsync {unstkret.TxHash}");
            await w.SyncAsync(null);
            var nb = balance + 2000m - 2;// * 0.988m; // two send fee
            //Assert.AreEqual(nb, w.BaseBalance);

            var stk2 = await w.GetStakingAsync(stkid);
            Assert.AreEqual((stk2 as TransactionBlock).Balances["LYR"].ToBalanceDecimal(), 0);

            var unstkretx = await w.UnStakingAsync(stkid);
            await WaitWorkflow(unstkretx.TxHash, $"UnStakingAsync {unstkretx.TxHash}", APIResultCodes.InvalidUnstaking);
            //await WaitBlock($"UnStakingAsync {unstkret.TxHash}");
            Assert.IsTrue(unstkretx.Successful());
        }

        private async Task TestProfitingAndStaking()
        {
            var shareRito = 0.5m;
            var totalProfit = 10000m;

            // create a profiting account
            Console.WriteLine("Profiting gen");
            var crpftret = await testWallet.CreateProfitingAccountAsync($"moneycow{_rand.Next()}", ProfitingType.Merchant,
                shareRito, 50);
            Assert.IsTrue(crpftret.Successful(), $"Can't create profiting: {crpftret.ResultCode}");
            await WaitWorkflow(crpftret.TxHash, "Create profiting account");
            var pftblocks = await testWallet.GetBrokerAccountsAsync<ProfitingGenesis>();
            var pftblock = pftblocks.FirstOrDefault();
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
            await WaitWorkflow(getpftRet.TxHash, "CreateDividendsAsync");

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

            var poole = await testWallet.GetLiquidatePoolAsync(token0, "LYR");
            if(poole.PoolAccountId == null)
            //if (networkId == "xtest")
            {
                var result0 = await testWallet.CreateTokenAsync(secs0[1], secs0[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result0.Successful(), "Failed to create token: " + result0.ResultCode);
                await testWallet.SyncAsync(null);

                var secs1 = token1.Split('/');
                var result1 = await testWallet.CreateTokenAsync(secs1[1], secs1[0], "", 8, 50000000000, true, "", "", "", Lyra.Core.Blocks.ContractTypes.Cryptocurrency, null);
                Assert.IsTrue(result0.Successful(), "Failed to create token: " + result1.ResultCode);
                await testWallet.SyncAsync(null);
                var crplret = await testWallet.CreateLiquidatePoolAsync(token0, "LYR");
                Assert.IsTrue(crplret.Successful(), $"Error create liquidate pool {crplret.ResultCode}");
                await WaitWorkflow(crplret.TxHash, "CreateLiquidatePoolAsync");
            }

            var pool = await testWallet.GetLiquidatePoolAsync(token0, "LYR");
            Assert.IsTrue(pool.PoolAccountId != null && pool.PoolAccountId.StartsWith('L'), "Can't get pool created.");

            // add liquidate to pool
            var addpoolret = await testWallet.AddLiquidateToPoolAsync(token0, 1000000, "LYR", 5000);
            Assert.IsTrue(addpoolret.Successful());

            await WaitWorkflow(addpoolret.TxHash, $"AddLiquidateToPoolAsync {addpoolret.TxHash}");

            // swap
            var poolx = await client.GetPoolAsync(token0, LyraGlobal.OFFICIALTICKERCODE);
            Assert.IsNotNull(poolx.PoolAccountId);
            var poolLatestBlock = poolx.GetBlock() as TransactionBlock;

            await testWallet.SyncAsync(null);

            var oldtkn0 = testWallet.GetLastSyncBlock().Balances[token0].ToBalanceDecimal();
            var cal2 = new SwapCalculator(LyraGlobal.OFFICIALTICKERCODE, token0, poolLatestBlock, LyraGlobal.OFFICIALTICKERCODE, 20, 0);
            var swapret = await testWallet.SwapTokenAsync("LYR", token0, "LYR", 20, cal2.SwapOutAmount);
            Assert.IsTrue(swapret.Successful());
            await WaitWorkflow(swapret.TxHash, $"SwapTokenAsync {swapret.TxHash}");

            await testWallet.SyncAsync(null);

            var gotamount = testWallet.GetLastSyncBlock().Balances[token0].ToBalanceDecimal() - oldtkn0;
            Console.WriteLine($"Got swapped amount {gotamount} {token0}");

            // remove liquidate from pool
            var rmliqret = await testWallet.RemoveLiquidateFromPoolAsync(token0, "LYR");
            Assert.IsTrue(rmliqret.Successful());
            await WaitWorkflow(rmliqret.TxHash, "remove liquidate from pool");

            await testWallet.SyncAsync(null);
        }

        private async Task TestDealerAsync()
        {
            var url = "https://dealerdevnet.lyra.live";
            dealer = new DealerClient(new Uri(new Uri(url), "/api/dealer/"));
            var dealerAbi = new Wallet.LyraContractABI
            {
                svcReq = BrokerActions.BRK_DLR_CREATE,
                targetAccountId = LyraGlobal.GUILDACCOUNTID,
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
            var dlrchkret = await testWallet.RPC.GetDealerByAccountIdAsync(testWallet.AccountId);
            if (!dlrchkret.Successful())
            {
                var ret = await testWallet.ServiceRequestAsync(dealerAbi);
                Assert.IsTrue(ret.Successful(), $"unable to create dealer: {ret.ResultCode}");
                await WaitWorkflow(ret.TxHash, $"Create Dealer");

                await testWallet.SyncAsync();
                var cp0 = testWallet.BaseBalance;

                var ret2 = await testWallet.ServiceRequestAsync(dealerAbi);
                Assert.IsTrue(ret2.Successful(), $"wrong create but should success: {ret2.ResultCode}");
                await WaitWorkflow(ret2.TxHash, $"Create Dealer 2", APIResultCodes.DuplicateName);

                await testWallet.SyncAsync();
                var cp1 = testWallet.BaseBalance;
                Assert.AreEqual(cp0 - 1, cp1, $"WF Refund is not OK. diff: {cp0 - cp1}");
            }

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
            if (test3Wallet.BaseBalance < 10000)
            {
                await genesisWallet.SendAsync(1000000, test3Wallet.AccountId);
                await test3Wallet.SyncAsync();
            }                

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

        private async Task<string> CreateTotMetaDataAsync(string netid, Wallet ownerWallet, HoldTypes totType,
            string name,
            string description,
            dynamic properties
            )
        {
            var ac = new AcademyClient(netid);

            // try to sign the request
            var lsb = await client.GetLastServiceBlockAsync();
            var input = $"{ownerWallet.AccountId}:{lsb.GetBlock().Hash}:{name}:{description}";
            var signature = Signatures.GetSignature(ownerWallet.PrivateKey, input, ownerWallet.AccountId);
            var retJson = await ac.CreateTotMetaAsync(ownerWallet.AccountId, signature, totType, name, description);
            var dynret = JsonConvert.DeserializeObject<dynamic>(retJson);

            Assert.IsTrue(dynret.ret == "Success");

            var metaurl = Convert.ToString(dynret.result);
            var meta = await ac.GetObjectAsync<TOTMeta>(metaurl);
            Assert.IsTrue(meta != null);
            Assert.AreEqual(meta.name, meta.name);
            Assert.AreEqual(meta.description, meta.description);
            return metaurl;
        }

        /// <summary>
        /// create tot to sell
        /// </summary>
        /// <param name="ownerWallet"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public async Task<TokenGenesisBlock> CreateTestToTAsync(string netid, Wallet ownerWallet, HoldTypes type, string metauri)
        {
            //var ticker = "nft/a346b16b-ca6c-4c86-9519-1e72fd517e9B";
            //var retg = await ownerWallet.RPC.GetTokenGenesisBlockAsync(testPublicKey, ticker, "aaa");
            //Assert.IsTrue(retg.Successful());
            //return;
            //await BurnAllNFT();
            //return;

            var rand = new Random();

            var name = $"a great ToT ({rand.NextInt64()})";
            var ret = await ownerWallet.CreateTOTAsync(type, name, "a tot for unit test", 1000, metauri, null);
            Assert.IsTrue(ret.Successful(), $"Create TOT failed: {ret.ResultMessage}");

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
            Assert.IsTrue(sendRet.ResultCode == APIResultCodes.TotTransferNotAllowed, $"Should faid to send ToT {type}: {sendRet.ResultCode}");
            var sendBlock = ownerWallet.GetLastSyncBlock();
            ResetAuthFail();

            return nftgen;
            //await BurnAllNFT();
            //name = $"a great nft ({rand.NextInt64()})";
            //ret = await ownerWallet.CreateNFTAsync(name, "a nft for unit test", 10, metauri);
            //Assert.IsTrue(ret.Successful(), $"Create NFT failed: {ret.ResultMessage}");
        }
    }
}
