using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.WorkFlow.STK;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Shared;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Lyra.Core.WorkFlow.PFT
{
    [LyraWorkFlow]//v
    public class WFCreateDividend : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_PFT_GETPFT,
                RecvVia = BrokerRecvType.GuildRecv,
            };
        }

        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var block = context.Send;
            TransactionBlock lastBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;
            var chgs = block.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            var pftid = block.Tags.ContainsKey("pftid") ? block.Tags["pftid"] : null;
            if (pftid == null)
                return APIResultCodes.InvalidAccountId;

            var pft = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
            if (pft == null)
                return APIResultCodes.InvalidAccountId;

            //var stkrs = sys.Storage.FindAllStakings(pftid, DateTime.UtcNow);
            if (/*!stkrs.Any(a => a.OwnerAccount == block.AccountID) && */pft.OwnerAccountId != block.AccountID)
                return APIResultCodes.RequestNotPermited;

            return APIResultCodes.Success;
        }

        #region Get profit
        public static async Task<TransactionBlock> SyncNodeFeesAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            Console.WriteLine("CR Dividend: SyncNodeFeesAsync in...");
            var nodeid = send.AccountID;

            // must be first profiting account of nodes'
            var pfts = await sys.Storage.FindAllProfitingAccountForOwnerAsync(nodeid);
            var pft = pfts.First();

            var usf = await sys.Storage.FindUnsettledFeesAsync(nodeid, pft.AccountID);
            if (usf == null)
                return null;

            Console.WriteLine("CR Dividend: SyncNodeFeesAsync, yes, have fee");

            var feesEndSb = await sys.Storage.FindServiceBlockByIndexAsync(usf.ServiceBlockEndHeight);

            TransactionBlock latestBlock = await sys.Storage.FindLatestBlockAsync(pft.AccountID) as TransactionBlock;
            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var receiveBlock = new ReceiveNodeProfitBlock
            {
                AccountID = pft.AccountID,
                ServiceHash = sb.Hash,
                //SourceHash = feesEndSb.Hash,      // no source like all genesis. set source to svc block vaoliate the rule.
                ServiceBlockStartHeight = usf.ServiceBlockStartHeight,
                ServiceBlockEndHeight = usf.ServiceBlockEndHeight,
                Balances = latestBlock.Balances.ToDictionary(entry => entry.Key,
                                           entry => entry.Value),
                Fee = 0,
                FeeType = AuthorizationFeeTypes.NoFee,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                NonFungibleToken = null,

                // profit specified config
                Name = ((IBrokerAccount)latestBlock).Name,
                OwnerAccountId = ((IBrokerAccount)latestBlock).OwnerAccountId,
                PType = ((IProfiting)latestBlock).PType,
                ShareRito = ((IProfiting)latestBlock).ShareRito,
                Seats = ((IProfiting)latestBlock).Seats,
                RelatedTx = send.Hash
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, context.State.ToString());

            if (latestBlock.Balances.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
            {
                receiveBlock.Balances[LyraGlobal.OFFICIALTICKERCODE] += usf.TotalFees.ToBalanceLong();
            }
            else
            {
                receiveBlock.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, usf.TotalFees.ToBalanceLong());
            }

            receiveBlock.InitializeBlock(latestBlock, sys.PosWallet.PrivateKey, AccountId: sys.PosWallet.AccountId);

            return receiveBlock;
        }

        // like wallet.receive
        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, LyraContext context)
        {
            Console.WriteLine("CR Dividend: BrokerOpsAsync");
            // if is current authorizers, sync fee first
            // add check to save resources
            var pftid = context.Send.Tags["pftid"];
            var pftgen = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
            if (pftgen.OwnerAccountId != context.Send.AccountID)
                return null;

            if (pftgen.PType == ProfitingType.Node)
            {
                var feeBlk = await SyncNodeFeesAsync(sys, context);
                if (feeBlk != null)
                    return feeBlk;
            }

            var transfer_info = await GetSendToPftAsync(sys, pftid);

            if (transfer_info.Successful())
            {
                var receiveBlock = await CNOReceiveProfitAsync(sys, context, context.Send.Hash, pftid, transfer_info);

                return receiveBlock;        // because we do it one block a time
            }
            else
                return null;        // the check
        }

        private static async Task<NewTransferAPIResult2> GetSendToPftAsync(DagSystem sys, string pftid)
        {
            Console.WriteLine("CR Dividend: GetSendToPftAsync");
            NewTransferAPIResult2 transfer_info = new NewTransferAPIResult2();
            SendTransferBlock sendBlock = await sys.Storage.FindUnsettledSendBlockAsync(pftid);

            if (sendBlock != null)
            {
                TransactionBlock previousBlock = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
                if (previousBlock == null)
                    transfer_info.ResultCode = APIResultCodes.CouldNotTraceSendBlockChain;
                else
                {
                    transfer_info.Transfer = sendBlock.GetBalanceChanges(previousBlock); //CalculateTransaction(sendBlock, previousSendBlock);
                    transfer_info.SourceHash = sendBlock.Hash;
                    transfer_info.NonFungibleToken = sendBlock.NonFungibleToken;
                    transfer_info.ResultCode = APIResultCodes.Success;
                }
            }
            else
                transfer_info.ResultCode = APIResultCodes.NoNewTransferFound;
            return transfer_info;
        }

        private static async Task<TransactionBlock> CNOReceiveProfitAsync(DagSystem sys, LyraContext context, string relatedTx, string pftid, NewTransferAPIResult2 transInfo)
        {
            Console.WriteLine("CR Dividend: CNOReceiveProfitAsync");

            var sb = await sys.Storage.GetLastServiceBlockAsync();
            var lastPft = await sys.Storage.FindLatestBlockAsync(pftid) as TransactionBlock;

            var pftNext = new ProfitingBlock
            {
                AccountID = lastPft.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastPft.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                SourceHash = transInfo.SourceHash,

                // profit specified config
                Name = ((IBrokerAccount)lastPft).Name,
                OwnerAccountId = ((IBrokerAccount)lastPft).OwnerAccountId,
                PType = ((IProfiting)lastPft).PType,
                ShareRito = ((IProfiting)lastPft).ShareRito,
                Seats = ((IProfiting)lastPft).Seats,
                RelatedTx = relatedTx
            };

            var recvBalances = lastPft.Balances.ToDecimalDict();
            foreach (var chg in transInfo.Transfer.Changes)
            {
                if (recvBalances.ContainsKey(chg.Key))
                    recvBalances[chg.Key] += chg.Value;
                else
                    recvBalances.Add(chg.Key, chg.Value);
            }

            pftNext.Balances = recvBalances.ToLongDict();

            pftNext.AddTag(Block.MANAGEDTAG, context.State.ToString());

            // pool blocks are service block so all service block signed by leader node
            pftNext.InitializeBlock(lastPft, sys.PosWallet.PrivateKey, AccountId: sys.PosWallet.AccountId);

            return pftNext;
        }

        public override async Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, LyraContext context, string reqHash)
        {
            Console.WriteLine("CR Dividend: ExtraOpsAsync");

            var reqBlock = await sys.Storage.FindBlockByHashAsync(reqHash);
            var pftid = reqBlock.Tags["pftid"];
            // create [multiple] send based on the staking
            // TODO: get staking by time receiving.
            // TODO: support bulk receive and single send
            // get stakings
            var lastBlock = await sys.Storage.FindLatestBlockAsync(pftid) as IProfiting;
            var stakers = sys.Storage.FindAllStakings(pftid, reqBlock.TimeStamp);
            var targets = stakers.Take(lastBlock.Seats);
            var relatedTxs = (await sys.Storage.FindBlocksByRelatedTxAsync(reqHash))
                .Cast<TransactionBlock>()
                .OrderBy(a => a.TimeStamp).ToList();
            if (relatedTxs.Count == 0)
            {
                // no balance
                return null;
            }
            // be carefull a profiting account may have no stakers.
            var totalStakingAmount = stakers.Sum(a => a.Amount);

            var allSends = new List<TransactionBlock>();
            var sentBlocks = relatedTxs.Where(a => a is BenefitingBlock)
                .Cast<BenefitingBlock>()
                .OrderBy(a => a.Height)
                .ToList();

            var lastProfitingBlock = relatedTxs.Where(a => a is ProfitingBlock)
                .OrderBy(a => a.TimeStamp)
                .LastOrDefault() as TransactionBlock;

            if(lastProfitingBlock == null)
            {
                // ops!
                return null;
            }
            var profitToDistribute = lastProfitingBlock.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal() * lastBlock.ShareRito;

            // don't distribute < 1LYR
            if (profitToDistribute >= 1 && totalStakingAmount > 0)
            {
                // create a dictionary to hold amounts to send
                // staking account -> amount
                var sendAmounts = new Dictionary<string, decimal>();
                foreach (var target in targets)
                {
                    var amount = Math.Round(profitToDistribute * (target.Amount / totalStakingAmount), 8, MidpointRounding.ToZero);
                    if (amount > 0.00000001m)
                        sendAmounts.Add(target.StkAccount, amount);
                }

                foreach (var target in targets)
                {
                    var stkSend = sentBlocks.FirstOrDefault(a => a.StakingAccountId == target.StkAccount);
                    if (stkSend != null)
                        continue;

                    if (!sendAmounts.ContainsKey(target.StkAccount))
                        continue;

                    var amount = sendAmounts[target.StkAccount];
                    var sb = await sys.Storage.GetLastServiceBlockAsync();
                    var lastblkx = await sys.Storage.FindLatestBlockAsync(pftid);

                    Console.WriteLine($"CreateBenefiting for {reqHash}");
                    var pftSend = CreateBenefiting(context, lastblkx as TransactionBlock, sb,
                        target, reqHash,
                        amount, sentBlocks.Count == targets.Count() * 2);

                    Console.WriteLine($"CreateBenefiting for {reqHash} generated {pftSend?.Hash} to {pftSend.DestinationAccountId}");
                    return pftSend;
                }

                // then create compound staking
                foreach (var target in targets)
                {
                    if (target.CompoundMode)
                    {
                        //var stkSend = sentBlocks.FirstOrDefault(a => a.StakingAccountId == target.StkAccount);
                        //if (stkSend == null)
                        //    continue;

                        //if (relatedTxs.Any(a => a is StakingBlock stk && stk.SourceHash == stkSend.Hash))
                        //    continue;

                        // look for any unsettled receive
                        Console.WriteLine($"CNOAddStakingImplAsync for {reqHash}");
                        SendTransferBlock xsend = await NodeService.Dag.Storage.FindUnsettledSendBlockAsync(target.StkAccount);
                        if (xsend == null)
                            continue;

                        var compstk = await WFStakingAddStaking.CNOAddStakingImplAsync(sys, context, xsend, reqHash);

                        Console.WriteLine($"CNOAddStakingImplAsync for {reqHash} generated {compstk?.Hash}");
                        if (compstk != null)
                            return compstk;
                    }
                }
            }

            // if share 100%, no need to send
            var pftgen = await sys.Storage.FindFirstBlockAsync(pftid) as ProfitingGenesis;
            if (pftgen.ShareRito == 1m)
                return null;

            // all remaining send to the owner
            if (sentBlocks.Any(a => a.DestinationAccountId == lastBlock.OwnerAccountId && a.StakingAccountId == null))
                return null;

            var sb2 = await sys.Storage.GetLastServiceBlockAsync();
            var lastblk = await sys.Storage.FindLatestBlockAsync(pftid) as TransactionBlock;
            var ownrSend = CreateBenefiting(context, lastblk, sb2, new Staker { OwnerAccount = lastBlock.OwnerAccountId }, reqHash, lastblk.Balances[LyraGlobal.OFFICIALTICKERCODE].ToBalanceDecimal()
                , sentBlocks.Count == targets.Count() * 2);

            return ownrSend;
        }

        private static BenefitingBlock CreateBenefiting(LyraContext context, TransactionBlock lastPft, ServiceBlock sb,
            Staker target, string relatedTx,
            decimal amount, bool finalOne
            )
        {
            Console.WriteLine("CR Dividend: CreateBenefiting");

            var pftSend = new BenefitingBlock
            {
                Height = lastPft.Height + 1,
                Name = ((IBrokerAccount)lastPft).Name,
                OwnerAccountId = ((IBrokerAccount)lastPft).OwnerAccountId,
                AccountID = lastPft.AccountID,
                Balances = new Dictionary<string, long>(),
                PreviousHash = lastPft.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,
                DestinationAccountId = target.CompoundMode ? target.StkAccount : target.OwnerAccount,

                // profit specified config
                PType = ((IProfiting)lastPft).PType,
                ShareRito = ((IProfiting)lastPft).ShareRito,
                Seats = ((IProfiting)lastPft).Seats,
                RelatedTx = relatedTx,
                StakingAccountId = target.StkAccount
            };

            //TODO: think about multiple token

            var lastBalance = lastPft.Balances[LyraGlobal.OFFICIALTICKERCODE];
            lastBalance -= amount.ToBalanceLong();
            pftSend.Balances.Add(LyraGlobal.OFFICIALTICKERCODE, lastBalance);

            pftSend.AddTag(Block.MANAGEDTAG, context.State.ToString());

            // pool blocks are service block so all service block signed by leader node
            pftSend.InitializeBlock(lastPft, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);

            Console.WriteLine($"Benefiting {pftSend.DestinationAccountId.Shorten()} Index {pftSend.Height} who is staking {target.Amount} {amount} LYR hash: {pftSend.Hash} signed by {NodeService.Dag.PosWallet.AccountId.Shorten()}");

            return pftSend;
        }

        #endregion
    }
}
