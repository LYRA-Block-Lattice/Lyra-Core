using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Core.WorkFlow.Shared;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.API.WorkFlow.UniMarket;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Generators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkflowCore.Interface;
using WorkflowCore.Models;
using static Microsoft.AspNetCore.Hosting.Internal.HostingApplication;

namespace Lyra.Core.WorkFlow
{
    public class WorkFlowDescription
    {
        public string Action { get; set; }
        public BrokerRecvType RecvVia { get; set; }

        // use steps to avoid checking block exists every time.
        public Func<DagSystem, LyraContext, Task<TransactionBlock?>>[] Steps { get; set; }
    }

    public abstract class WorkFlowBase : DebiWorkflow, IDebiWorkFlow, IWorkflow<LyraContext>
    {
        // IWorkflow<LyraContext>
        public string Id => GetDescription().Action;
        public int Version => 1;

        // IDebiWorkflow
        public abstract WorkFlowDescription GetDescription();

        // for workflow that steps depends on the send block.
        public virtual Task<Func<DagSystem, LyraContext, Task<TransactionBlock>>[]> GetProceduresAsync(DagSystem sys, LyraContext context)
        {
            return Task.FromResult(GetDescription().Steps);
        }
        public virtual async Task<TransactionBlock> MainProcAsync(DagSystem sys, LyraContext context)
        {            
            return await BrokerOpsAsync(sys, context) ?? await ExtraOpsAsync(sys, context.Send.Hash);
        }
        public virtual async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, LyraContext context)
        {
            return await OneByOneAsync(sys, context, await GetProceduresAsync(sys, context));
        }
        public virtual Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string hash)
        {
            return Task.FromResult((TransactionBlock)null);
        }

        private async Task<ReceiveTransferBlock?> DefaultReceiveAsync(DagSystem sys, LyraContext context, WFState nextState)
        {
            var desc = GetDescription();
            return desc.RecvVia switch
            {
                BrokerRecvType.GuildRecv => await TransReceiveAsync<GuildRecvBlock>(sys,
                    context.Send.Hash, context.Send, LyraGlobal.GUILDACCOUNTID, nextState),
                BrokerRecvType.DaoRecv => await TransReceiveAsync<DaoRecvBlock>(sys,
                    context.Send.Hash, context.Send, context.Send.DestinationAccountId, nextState),
                _ => throw new NotImplementedException($"Should override NormalReceiveAsync and RefundReceiveAsync about in WF {desc.Action}")
            };
        }

        public virtual async Task<ReceiveTransferBlock?> NormalReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await DefaultReceiveAsync(sys, context, WFState.Running);
        }

        public virtual async Task<ReceiveTransferBlock?> RefundReceiveAsync(DagSystem sys, LyraContext context)
        {
            return await DefaultReceiveAsync(sys, context, WFState.Refund);
        }

        public virtual async Task<SendTransferBlock?> RefundSendAsync(DagSystem sys, LyraContext context)
        {
            var desc = GetDescription();
            string? srcAccount = null;
            if(desc.RecvVia == BrokerRecvType.GuildRecv)
            {
                srcAccount = LyraGlobal.GUILDACCOUNTID;
            }
            else
            {
                srcAccount = context.Send.DestinationAccountId;
            }

            if (srcAccount== null)
                throw new NotImplementedException($"Should override RefundSendAsync about in WF {desc.Action}");

            var last1 = await sys.Storage.FindLatestBlockAsync(srcAccount) as TransactionBlock;
            var last2 = await sys.Storage.FindBlockByHashAsync(last1.PreviousHash) as TransactionBlock;
            var chgs = last1.GetBalanceChanges(last2);

            if(srcAccount == LyraGlobal.GUILDACCOUNTID)
                return await TransSendAsync<GuildSendBlock>(sys,
                    context.Send.Hash, LyraGlobal.GUILDACCOUNTID, context.Send.AccountID,
                    chgs.Changes,
                    WFState.Finished);

            if(last1 is IDao)
                return await TransSendAsync<DaoSendBlock>(sys,
                    context.Send.Hash, LyraGlobal.GUILDACCOUNTID, context.Send.AccountID,
                    chgs.Changes,
                    WFState.Finished);

            if(last1 is IUniTrade)
                return await TransSendAsync<UniTradeSendBlock>(sys,
                    context.Send.Hash, LyraGlobal.GUILDACCOUNTID, context.Send.AccountID,
                    chgs.Changes,
                    WFState.Finished);

            // TODO: support pool, dex, etc.
            throw new NotImplementedException();
        }

        //public async Task<TransactionBlock> UnReceiveAsync(DagSystem sys, SendTransferBlock send)
        //{
        //    if(GetDescription().RecvVia == BrokerRecvType.None)
        //    {
        //        throw new Exception("Must override UnReceiveAsync");
        //    }
        //    var block =
        //        await BrokerOperations.RefundViaCallback[GetDescription().RecvVia](DagSystem.Singleton, send);
        //    return block;
        //}

        public virtual async Task<WorkflowAuthResult> PreAuthAsync(DagSystem sys, LyraContext context)
        {
            List<string> lockedIDs = null;
            try
            {
                lockedIDs = await GetLockedAccountIdsAsync(sys, context.Send);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error GetLockedAccountIds: {ex}");
            }

            if(lockedIDs == null)
            {
                return new WorkflowAuthResult
                {
                    LockedIDs = new List<string>(),
                    Result = APIResultCodes.InvalidOperation,
                };
            }

            foreach (var lockedId in lockedIDs)
            {
                if (ConsensusService.Singleton.CheckIfIdIsLocked(lockedId))
                {
                    Console.WriteLine($"Resource is busy for workflow: {lockedId}");
                    return new WorkflowAuthResult
                    {
                        LockedIDs = lockedIDs,
                        Result = APIResultCodes.ResourceIsBusy,
                    };
                }
            }

            return new WorkflowAuthResult
            {
                LockedIDs = lockedIDs,
                Result = await PreSendAuthAsync(sys, context)
            };
        }

        public static async Task<LockerDTO> GetLocketDTOAsync(DagSystem sys, TransactionBlock trans)
        {
            var strs = new List<string>();
            string action = null;
            if (trans is SendTransferBlock send)
            {                
                if (send.Tags != null && send.Tags.ContainsKey(Block.REQSERVICETAG))
                    action = send.Tags[Block.REQSERVICETAG];

                string brkaccount, brkaccount2 = null, brkaccount3 = null;
                switch (action)
                {
                    // profiting, create dividends
                    case BrokerActions.BRK_PFT_GETPFT:
                        brkaccount = send.Tags["pftid"];
                        break;

                    // pool
                    case BrokerActions.BRK_POOL_ADDLQ:
                    case BrokerActions.BRK_POOL_SWAP:
                    case BrokerActions.BRK_POOL_RMLQ:
                        brkaccount = send.Tags["poolid"];
                        break;

                    // staking
                    case BrokerActions.BRK_STK_ADDSTK:
                        brkaccount = send.DestinationAccountId;
                        break;
                    case BrokerActions.BRK_STK_UNSTK:
                        brkaccount = send.Tags["stkid"];
                        break;

                    // DEX
                    //case BrokerActions.BRK_DEX_DPOREQ:
                    case BrokerActions.BRK_DEX_MINT:
                    case BrokerActions.BRK_DEX_GETTKN:
                    case BrokerActions.BRK_DEX_PUTTKN:
                    case BrokerActions.BRK_DEX_WDWREQ:
                        brkaccount = send.Tags["dexid"];
                        break;

                    // DAO
                    //case BrokerActions.BRK_DAO_CRDAO:
                    case BrokerActions.BRK_DAO_JOIN:
                    case BrokerActions.BRK_DAO_LEAVE:
                        brkaccount = send.Tags["daoid"];
                        break;

                    case BrokerActions.BRK_DAO_CHANGE:
                    case BrokerActions.BRK_DAO_VOTED_CHANGE:
                        brkaccount = send.DestinationAccountId;
                        break;

                    // OTC
                    case BrokerActions.BRK_OTC_CRODR:
                        var order = JsonConvert.DeserializeObject<OTCOrder>(send.Tags["data"]);
                        brkaccount = order.daoId;
                        break;

                    case BrokerActions.BRK_OTC_CRTRD:
                        var trade = JsonConvert.DeserializeObject<OTCTrade>(send.Tags["data"]);
                        brkaccount = trade.daoId;
                        brkaccount2 = trade.orderId;
                        break;

                    case BrokerActions.BRK_OTC_TRDPAYSENT:
                    case BrokerActions.BRK_OTC_TRDPAYGOT:
                        brkaccount = send.Tags["tradeid"];
                        break;

                    case BrokerActions.BRK_OTC_TRDCANCEL:
                        brkaccount = send.Tags["tradeid"];
                        brkaccount2 = send.Tags["orderid"];
                        brkaccount3 = send.Tags["daoid"];
                        break;

                    case BrokerActions.BRK_OTC_ORDDELST:
                    case BrokerActions.BRK_OTC_ORDCLOSE:
                        brkaccount = send.Tags["orderid"];
                        brkaccount2 = send.Tags["daoid"];
                        break;

                    // OTC Dispute
                    case BrokerActions.BRK_OTC_CRDPT:
                    case BrokerActions.BRK_OTC_RSLDPT:
                        brkaccount = send.DestinationAccountId; // trade id
                        var tradeDspt = (await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId)) as IOtcTrade;
                        brkaccount2 = tradeDspt.Trade.daoId;
                        break;

                    // Voting
                    case BrokerActions.BRK_VOT_CREATE:
                        var subject = JsonConvert.DeserializeObject<VotingSubject>(send.Tags["data"]);
                        brkaccount = subject.DaoId;
                        break;

                    case BrokerActions.BRK_VOT_VOTE:
                    case BrokerActions.BRK_VOT_CLOSE:
                        brkaccount = send.Tags["voteid"];
                        break;

                    case BrokerActions.BRK_DAO_CRDAO:
                    case BrokerActions.BRK_POOL_CRPL:
                    case BrokerActions.BRK_PFT_CRPFT:
                    case BrokerActions.BRK_STK_CRSTK:
                    case BrokerActions.BRK_DEX_DPOREQ:
                    case BrokerActions.BRK_DLR_CREATE:
                        brkaccount = null;
                        break;

                    #region universal trade
                    case BrokerActions.BRK_UNI_CRODR:
                        var uorder = JsonConvert.DeserializeObject<UniOrder>(send.Tags["data"]);
                        brkaccount = uorder.daoId;
                        break;

                    case BrokerActions.BRK_UNI_CRTRD:
                        var utrade = JsonConvert.DeserializeObject<UniTrade>(send.Tags["data"]);
                        brkaccount = utrade.daoId;
                        brkaccount2 = utrade.orderId;
                        break;

                    case BrokerActions.BRK_UNI_TRDPAYSENT:
                    case BrokerActions.BRK_UNI_TRDPAYGOT:
                        brkaccount = send.Tags["tradeid"];
                        break;

                    case BrokerActions.BRK_UNI_TRDCANCEL:
                        brkaccount = send.Tags["tradeid"];
                        brkaccount2 = send.Tags["orderid"];
                        brkaccount3 = send.Tags["daoid"];
                        break;

                    case BrokerActions.BRK_UNI_ORDDELST:
                    case BrokerActions.BRK_UNI_ORDCLOSE:
                        brkaccount = send.Tags["orderid"];
                        brkaccount2 = send.Tags["daoid"];
                        break;

                    // Uni Dispute
                    case BrokerActions.BRK_UNI_CRDPT:
                    case BrokerActions.BRK_UNI_RSLDPT:
                        brkaccount = send.DestinationAccountId; // trade id
                        var utradeDspt = (await sys.Storage.FindLatestBlockAsync(send.DestinationAccountId)) as IUniTrade;
                        brkaccount2 = utradeDspt.Trade.daoId;
                        break;
                    #endregion

                    case BrokerActions.BRK_DLR_UPDATE:
                        brkaccount = null;
                        break;

                    default:
                        if(action != null)
                            Console.WriteLine($"Unknown REQ Action: {action}");
                        brkaccount = null;
                        break;
                };

                
                if (brkaccount != null)
                    strs.Add(brkaccount);
                if (brkaccount2 != null)
                    strs.Add(brkaccount2);
                if (brkaccount3 != null)
                    strs.Add(brkaccount3);

                strs.Add(send.AccountID);       // itself
            }
            else if (trans is ReceiveTransferBlock recv)
            {
                strs = new List<string> { recv.AccountID };
            }

            return new LockerDTO
            {
                reqhash = trans.Hash,
                haswf = action != null,
                lockedups = strs,
                seqhashes = new List<string>()
            };
        }

        public virtual async Task<List<string>> GetLockedAccountIdsAsync(DagSystem sys, TransactionBlock trans)
        {
            var dto = await GetLocketDTOAsync(sys, trans);
            return dto.lockedups;
        }
        public abstract Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context);

        protected async Task<TransactionBlock> OneByOneAsync(DagSystem sys, LyraContext context,
            params Func<DagSystem, LyraContext, Task<TransactionBlock>>[] operations)
        {
            var send = context.Send;
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var desc = GetDescription();

            var cnt = desc.RecvVia == BrokerRecvType.None || desc.RecvVia == BrokerRecvType.PFRecv;
            var index = blocks.Count - (cnt ? 0 : 1);
            if (index >= operations.Length)
            {
                Console.WriteLine($"{desc.Action} step: {index}/{operations.Length}");
                return null;
            }                

            Console.WriteLine($"{desc.Action} step: {index}/{operations.Length} {operations[index].Method.Name}");

            return await operations[index](sys, context);
        }

        protected async Task<TransactionBlock> TransactionOperateAsync(
            DagSystem sys,
            string relatedHash,
            TransactionBlock prevBlock,
            Func<TransactionBlock> GenBlock,
            Func<WFState> NewState,
            Action<TransactionBlock> ChangeBlock
            )
        {
            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var nextblock = GenBlock();

            // block
            nextblock.ServiceHash = lsb.Hash;
            nextblock.Tags = null;
            nextblock.AddTag(Block.MANAGEDTAG, NewState().ToString());

            // transactions
            nextblock.Balances = prevBlock.Balances.ToDecimalDict().ToLongDict();

            // ibroker
            (nextblock as IBrokerAccount).RelatedTx = relatedHash;

            if (ChangeBlock != null)
                ChangeBlock(nextblock);

            await nextblock.InitializeBlockAsync(prevBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            //remove this debug code
            //System.IO.File.AppendAllText("c:\\tmp\\hashs.txt", nextblock.GetHashInput() + "\n\n");

            return nextblock;
        }

        protected async Task<T> TransactionOperateAsync<T>(
            DagSystem sys,
            string relatedHash,
            TransactionBlock prevBlock,
            Func<T> GenBlock,
            Func<WFState> NewState,
            Action<T> ChangeBlock
            ) where T: TransactionBlock
        {
            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var nextblock = GenBlock();

            // block
            nextblock.ServiceHash = lsb.Hash;
            nextblock.Tags = null;
            nextblock.AddTag(Block.MANAGEDTAG, NewState().ToString());

            // transactions
            nextblock.Balances = prevBlock.Balances.ToDecimalDict().ToLongDict();

            // ibroker
            (nextblock as IBrokerAccount).RelatedTx = relatedHash;

            if (ChangeBlock != null)
                ChangeBlock(nextblock);

            await nextblock.InitializeBlockAsync(prevBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return nextblock;
        }

        async Task<T> TransReceiveAsync<T>(DagSystem sys, string key, SendTransferBlock send, string recvAccountId, WFState nextWFState) where T : TransactionBlock
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(send.Hash);
            if (recv != null)
                return null;

            var prevBlock = await sys.Storage.FindLatestBlockAsync(recvAccountId) as TransactionBlock;
            var txInfo = send.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock);

            return await TransactionOperateAsync<T>(sys, key, prevBlock,
                () => prevBlock.GenInc<T>(),
                () => nextWFState,
                (b) =>
                {
                    // recv
                    var recv = b as ReceiveTransferBlock;
                    recv.SourceHash = send.Hash;

                    var bal = recv.Balances.ToDecimalDict();
                    foreach(var tx in txInfo.Changes)
                    {
                        if (bal.ContainsKey(tx.Key))
                            bal[tx.Key] += tx.Value;
                        else
                            bal[tx.Key] = tx.Value;
                    }
                    
                    recv.Balances = bal.ToLongDict();
                });
        }

        async Task<T> TransSendAsync<T>(DagSystem sys, string key, string srcAccountId, string dstAccountId,
            Dictionary<string, decimal> amounts,
            WFState nextWFState) where T : TransactionBlock
        {
            // check exists
            var prevBlock = await sys.Storage.FindLatestBlockAsync(srcAccountId) as TransactionBlock;

            return await TransactionOperateAsync<T>(sys, key, prevBlock,
                () => prevBlock.GenInc<T>(),
                () => nextWFState,
                (b) =>
                {
                    // recv
                    var snd = b as SendTransferBlock;
                    snd.DestinationAccountId = dstAccountId;

                    var bal = snd.Balances.ToDecimalDict();
                    foreach (var tx in amounts)
                    {
                        bal[tx.Key] -= tx.Value;
                    }

                    snd.Balances = bal.ToLongDict();
                });
        }

        #region Receive svc request
        public static async Task<ReceiveTransferBlock> ReceivePoolFactoryFeeAsync(DagSystem sys, SendTransferBlock sendBlock, WorkflowAuthResult authResult)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            TransactionBlock prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            if (prevSend == null)
                return null;        // process missing block

            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var receiveBlock = new ReceiveAsFeeBlock
            {
                AccountID = sendBlock.DestinationAccountId,
                VoteFor = null,
                ServiceHash = lsb.Hash,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = Math.Round(txInfo.Changes[LyraGlobal.OFFICIALTICKERCODE], 8, MidpointRounding.ToZero),
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.FullFee,
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, authResult.Result == APIResultCodes.Success ?
                WFState.Running.ToString() : WFState.Refund.ToString());

            TransactionBlock latestPoolBlock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            var latestBalances = latestPoolBlock.Balances.ToDecimalDict();
            var recvBalances = latestPoolBlock.Balances.ToDecimalDict();

            receiveBlock.Balances = recvBalances.ToLongDict();

            await receiveBlock.InitializeBlockAsync(latestPoolBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return receiveBlock;
        }

        public static async Task<ReceiveTransferBlock> ReceiveDaoFeeAsync(DagSystem sys, SendTransferBlock sendBlock, WorkflowAuthResult authResult)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            var txInfo = sendBlock.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock);
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var receiveBlock = new DaoRecvBlock
            {
                // block
                ServiceHash = lsb.Hash,

                // transaction
                AccountID = sendBlock.DestinationAccountId,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // broker
                Name = ((IBrokerAccount)lastblock).Name,
                OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId,
                RelatedTx = sendBlock.Hash,

                // profiting
                PType = ((IProfiting)lastblock).PType,
                ShareRito = ((IProfiting)lastblock).ShareRito,
                Seats = ((IProfiting)lastblock).Seats,

                // dao     
                SellerFeeRatio = ((IDao)lastblock).SellerFeeRatio,
                BuyerFeeRatio = ((IDao)lastblock).BuyerFeeRatio,
                SellerPar = ((IDao)lastblock).SellerPar,
                BuyerPar = ((IDao)lastblock).BuyerPar,
                Treasure = ((IDao)lastblock).Treasure.ToDecimalDict().ToLongDict(),
                Description = ((IDao)lastblock).Description,
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, authResult.Result == APIResultCodes.Success ?
                WFState.Running.ToString() : WFState.Refund.ToString());

            var latestBalances = lastblock.Balances.ToDecimalDict();
            var recvBalances = lastblock.Balances.ToDecimalDict();
            foreach (var chg in txInfo.Changes)
            {
                if (recvBalances.ContainsKey(chg.Key))
                    recvBalances[chg.Key] += chg.Value;
                else
                    recvBalances.Add(chg.Key, chg.Value);
            }

            receiveBlock.Balances = recvBalances.ToLongDict();

            await receiveBlock.InitializeBlockAsync(lastblock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return receiveBlock;
        }

        public static async Task<ReceiveTransferBlock> ReceiveTradeFeeAsync(DagSystem sys, SendTransferBlock sendBlock, WorkflowAuthResult authResult)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            var txInfo = sendBlock.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock);
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var receiveBlock = new OtcTradeRecvBlock
            {
                // block
                ServiceHash = lsb.Hash,

                // transaction
                AccountID = sendBlock.DestinationAccountId,
                SourceHash = sendBlock.Hash,
                Balances = new Dictionary<string, long>(),
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // broker
                Name = ((IBrokerAccount)lastblock).Name,
                OwnerAccountId = ((IBrokerAccount)lastblock).OwnerAccountId,
                RelatedTx = sendBlock.Hash,

                // trade     
                Trade = ((IOtcTrade)lastblock).Trade,
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, authResult.Result == APIResultCodes.Success ?
                WFState.Running.ToString() : WFState.Refund.ToString());

            var latestBalances = lastblock.Balances.ToDecimalDict();
            var recvBalances = lastblock.Balances.ToDecimalDict();
            foreach (var chg in txInfo.Changes)
            {
                if (recvBalances.ContainsKey(chg.Key))
                    recvBalances[chg.Key] += chg.Value;
                else
                    recvBalances.Add(chg.Key, chg.Value);
            }

            receiveBlock.Balances = recvBalances.ToLongDict();

            await receiveBlock.InitializeBlockAsync(lastblock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return receiveBlock;
        }

        public static Task<ReceiveTransferBlock> ReceiveNoneAsync(DagSystem sys, SendTransferBlock sendBlock, WorkflowAuthResult authResult)
        {
            return Task.FromResult<ReceiveTransferBlock>(null);
        }
        #endregion
    }
}
