using DexServer.Ext;
using Lyra.Core.Accounts;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Lyra.Data.Blocks;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using Lyra.Data.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class BrokerOperations
    {
        // every method must check if the operation has been done.
        // if has been done, return null.
        public static async Task<ReceiveTransferBlock> ReceivePoolFactoryFeeAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            TransactionBlock prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
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

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            TransactionBlock latestPoolBlock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // ignore any token but LYR. keep the block clean.
            //if (!txInfo.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
            //    return;

            var latestBalances = latestPoolBlock.Balances.ToDecimalDict();
            var recvBalances = latestPoolBlock.Balances.ToDecimalDict();
            //foreach (var chg in txInfo.Changes)
            //{
            //    if (chg.Key != LyraGlobal.OFFICIALTICKERCODE)
            //        continue;

            //    if (recvBalances.ContainsKey(chg.Key))
            //        recvBalances[chg.Key] += chg.Value;
            //    else
            //        recvBalances.Add(chg.Key, chg.Value);
            //}

            receiveBlock.Balances = recvBalances.ToLongDict();

            receiveBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));

            return receiveBlock;
            //var tx = new ServiceWithActionTx(sendBlock.Hash)
            //{
            //    PoolId = latestPoolBlock.AccountID
            //};
            //await QueueBlockForPoolAsync(receiveBlock, tx);  // create pool / withdraw
        }

        public static async Task<ReceiveTransferBlock> ReceiveDaoFeeAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            TransactionBlock prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            var lsb = await sys.Storage.GetLastServiceBlockAsync();
            var receiveBlock = new ReceiveTransferBlock
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

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

            TransactionBlock latestPoolBlock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            // ignore any token but LYR. keep the block clean.
            //if (!txInfo.Changes.ContainsKey(LyraGlobal.OFFICIALTICKERCODE))
            //    return;

            var latestBalances = latestPoolBlock.Balances.ToDecimalDict();
            var recvBalances = latestPoolBlock.Balances.ToDecimalDict();
            //foreach (var chg in txInfo.Changes)
            //{
            //    if (chg.Key != LyraGlobal.OFFICIALTICKERCODE)
            //        continue;

            //    if (recvBalances.ContainsKey(chg.Key))
            //        recvBalances[chg.Key] += chg.Value;
            //    else
            //        recvBalances.Add(chg.Key, chg.Value);
            //}

            receiveBlock.Balances = recvBalances.ToLongDict();

            receiveBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));

            return receiveBlock;
            //var tx = new ServiceWithActionTx(sendBlock.Hash)
            //{
            //    PoolId = latestPoolBlock.AccountID
            //};
            //await QueueBlockForPoolAsync(receiveBlock, tx);  // create pool / withdraw
        }

    }
}
