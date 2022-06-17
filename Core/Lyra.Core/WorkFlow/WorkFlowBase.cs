using Lyra.Core.API;
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
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Lyra.Core.WorkFlow
{
    public class WorkFlowDescription
    {
        public string Action { get; set; }
        public BrokerRecvType RecvVia { get; set; }

        // use steps to avoid checking block exists every time.
        public Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[] Steps { get; set; }
    }

    public abstract class WorkFlowBase : DebiWorkflow, IDebiWorkFlow, IWorkflow<LyraContext>
    {
        // IWorkflow<LyraContext>
        public string Id => GetDescription().Action;
        public int Version => 1;

        // IDebiWorkflow
        public abstract WorkFlowDescription GetDescription();

        // for workflow that steps depends on the send block.
        public virtual Task<Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[]> GetProceduresAsync(DagSystem sys, SendTransferBlock send)
        {
            return Task.FromResult(GetDescription().Steps);
        }
        public virtual async Task<TransactionBlock> MainProcAsync(DagSystem sys, SendTransferBlock send, LyraContext context)
        {
            return await BrokerOpsAsync(sys, send) ?? await ExtraOpsAsync(sys, send.Hash);
        }
        public virtual async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, SendTransferBlock send)
        {
            return await OneByOneAsync(sys, send, await GetProceduresAsync(sys, send));
        }
        public virtual Task<TransactionBlock> ExtraOpsAsync(DagSystem sys, string hash)
        {
            return Task.FromResult((TransactionBlock)null);
        }

        public virtual async Task<WrokflowAuthResult> PreAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            List<string> lockedIDs = null;
            try
            {
                lockedIDs = await GetLockedAccountIds(sys, send, last);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error GetLockedAccountIds: {ex}");
            }

            if(lockedIDs == null)
            {
                return new WrokflowAuthResult
                {
                    LockedIDs = new List<string>(),
                    Result = APIResultCodes.InvalidOperation,
                };
            }

            foreach (var lockedId in lockedIDs)
            {
                if (ConsensusService.Singleton.CheckIfIdIsLocked(lockedId))
                {
                    return new WrokflowAuthResult
                    {
                        LockedIDs = lockedIDs,
                        Result = APIResultCodes.ResourceIsBusy,
                    };
                }
            }

            return new WrokflowAuthResult
            {
                LockedIDs = lockedIDs,
                Result = await PreSendAuthAsync(sys, send, last)
            };
        }

        private static List<string> GetBrokerAccountID(SendTransferBlock send)
        {
            string action = null;
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
                    brkaccount = send.DestinationAccountId;
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
                // 
                default:
                    Console.WriteLine($"Unknown REQ Action: {action}");
                    brkaccount = null;
                    break;
            };

            var strs = new List<string>();
            if(brkaccount != null)
                strs.Add(brkaccount);
            if(brkaccount2 != null)
                strs.Add(brkaccount2);
            if(brkaccount3 != null)
                strs.Add(brkaccount3);
            return strs;
        }

        public virtual Task<List<string>> GetLockedAccountIds(DagSystem sys, SendTransferBlock send, TransactionBlock last)
        {
            return Task.FromResult(GetBrokerAccountID(send));
        }
        public abstract Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock last);

        protected async Task<TransactionBlock> OneByOneAsync(DagSystem sys, SendTransferBlock send,
            params Func<DagSystem, SendTransferBlock, Task<TransactionBlock>>[] operations)
        {
            var blocks = await sys.Storage.FindBlocksByRelatedTxAsync(send.Hash);
            var desc = GetDescription();

            var cnt = desc.RecvVia == BrokerRecvType.None || desc.RecvVia == BrokerRecvType.PFRecv;
            var index = blocks.Count - (cnt ? 0 : 1);
            if (index >= operations.Length)
                return null;

            return await operations[index](sys, send);
        }

        protected async Task<TransactionBlock> TransactionOperateAsync(
            DagSystem sys,
            string relatedHash,
            Block prevBlock,
            Func<TransactionBlock> GenBlock,
            Action<TransactionBlock> ChangeBlock
            )
        {
            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var nextblock = GenBlock();

            // block
            nextblock.ServiceHash = lsb.Hash;
            nextblock.Tags?.Clear();
            nextblock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            // ibroker
            (nextblock as IBrokerAccount).RelatedTx = relatedHash;

            if (ChangeBlock != null)
                ChangeBlock(nextblock);

            await nextblock.InitializeBlockAsync(prevBlock, (hash) => Task.FromResult(Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId)));

            return nextblock;
        }
    }
}
