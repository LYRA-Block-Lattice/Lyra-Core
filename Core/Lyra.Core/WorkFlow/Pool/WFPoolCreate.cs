using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.WorkFlow.Pool
{
    [LyraWorkFlow]
    public class WFPoolCreate : WorkFlowBase
    {
        public override WorkFlowDescription GetDescription()
        {
            return new WorkFlowDescription
            {
                Action = BrokerActions.BRK_POOL_CRPL,
                RecvVia = BrokerRecvType.GuildRecv,
            };
        }

        protected static async Task<bool> CheckTokenAsync(DagSystem sys, string tokenName)
        {
            var tokn = await sys.Storage.FindTokenGenesisBlockAsync(null, tokenName);
            return tokn != null;
        }

        protected static async Task<APIResultCodes> CheckPoolTagsAsync(DagSystem sys, Block block, int tagsCount = 3)
        {
            if (block.Tags.ContainsKey("token0") && await CheckTokenAsync(sys, block.Tags["token0"])
                && block.Tags.ContainsKey("token1") && await CheckTokenAsync(sys, block.Tags["token1"])
                && block.Tags["token0"] != block.Tags["token1"]
                && (block.Tags["token0"] == LyraGlobal.OFFICIALTICKERCODE || block.Tags["token1"] == LyraGlobal.OFFICIALTICKERCODE)
                && block.Tags.Count == tagsCount
                )
                return APIResultCodes.Success;
            else
                return APIResultCodes.InvalidBlockTags;
        }

        #region BRK_POOL_CRPL
        public override async Task<APIResultCodes> PreSendAuthAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            // generic pool factory
            var tgc = await CheckPoolTagsAsync(sys, send);
            if (tgc != APIResultCodes.Success)
                return tgc;

            TransactionBlock lastBlock = await DagSystem.Singleton.Storage.FindBlockByHashAsync(send.PreviousHash) as TransactionBlock;

            var chgs = send.GetBalanceChanges(lastBlock);
            if (!chgs.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
                return APIResultCodes.InvalidFeeAmount;

            if (chgs.Changes.Count > 1)
                return APIResultCodes.InvalidFeeAmount;

            // check if pool exists
            var factory = await sys.Storage.GetPoolFactoryAsync();
            if (factory == null)
                return APIResultCodes.SystemNotReadyToServe;

            // action

            if (chgs.Changes[LyraGlobal.OFFICIALTICKERCODE] != PoolFactoryBlock.PoolCreateFee)
                return APIResultCodes.InvalidFeeAmount;

            var poolGenesis = await sys.Storage.GetPoolAsync(send.Tags["token0"], send.Tags["token1"]);
            if (poolGenesis != null)
                return APIResultCodes.PoolAlreadyExists;

            return APIResultCodes.Success;
        }

        public override async Task<TransactionBlock> BrokerOpsAsync(DagSystem sys, LyraContext context)
        {
            var send = context.Send;
            var pool = await sys.Storage.GetPoolAsync(send.Tags["token0"], send.Tags["token1"]);
            if (pool != null)
                return null;

            // get token gensis to make the token name proper
            var token0Gen = await sys.Storage.FindTokenGenesisBlockAsync(null, send.Tags["token0"]);
            var token1Gen = await sys.Storage.FindTokenGenesisBlockAsync(null, send.Tags["token1"]);

            //if (token0Gen == null || token1Gen == null)
            //{
            //    return;
            //}

            var arrStr = new[] { token0Gen.Ticker, token1Gen.Ticker };
            Array.Sort(arrStr);

            var poole = await sys.Storage.GetPoolAsync(arrStr[0], arrStr[1]);
            if (poole != null)
                return null;

            var sb = await sys.Storage.GetLastServiceBlockAsync();

            // create a semi random account for pool.
            // it can be verified by other nodes.
            var keyStr = $"{send.Hash.Substring(0, 16)},{arrStr[0]},{arrStr[1]},{send.AccountID}";
            var AccountId = Base58Encoding.EncodeAccountId(Encoding.ASCII.GetBytes(keyStr).Take(64).ToArray());

            var poolGenesis = new PoolGenesisBlock
            {
                Height = 1,
                AccountType = AccountTypes.Pool,
                AccountID = AccountId,        // in fact we not use this account.
                Balances = new Dictionary<string, long>(),
                PreviousHash = sb.Hash,
                ServiceHash = sb.Hash,
                Fee = 0,
                FeeCode = LyraGlobal.OFFICIALTICKERCODE,
                FeeType = AuthorizationFeeTypes.NoFee,

                // pool specified config
                Token0 = arrStr[0],
                Token1 = arrStr[1],
                RelatedTx = send.Hash
            };

            poolGenesis.AddTag(Block.MANAGEDTAG, context.State.ToString());

            // pool blocks are service block so all service block signed by leader node
            poolGenesis.InitializeBlock(null, NodeService.Dag.PosWallet.PrivateKey, AccountId: NodeService.Dag.PosWallet.AccountId);
            return poolGenesis;
            //await QueueTxActionBlockAsync(poolGenesis);
        }
        #endregion
    }
}
