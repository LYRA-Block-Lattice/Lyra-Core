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
using Lyra.Data.API.WorkFlow;

namespace Lyra.Core.Decentralize
{
    public class BrokerOperations
    {
        public static Dictionary<BrokerRecvType, Func<DagSystem, SendTransferBlock, Task<ReceiveTransferBlock>>> ReceiveViaCallback 
                => new Dictionary<BrokerRecvType, Func<DagSystem, SendTransferBlock, Task<ReceiveTransferBlock>>>
                {
                    { BrokerRecvType.PFRecv, ReceivePoolFactoryFeeAsync },
                    { BrokerRecvType.DaoRecv, ReceiveDaoFeeAsync },
                    { BrokerRecvType.TradeRecv, ReceiveTradeFeeAsync },
                    { BrokerRecvType.None, ReceiveNoneAsync },
                };

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

            var latestBalances = latestPoolBlock.Balances.ToDecimalDict();
            var recvBalances = latestPoolBlock.Balances.ToDecimalDict();

            receiveBlock.Balances = recvBalances.ToLongDict();

            receiveBlock.InitializeBlock(latestPoolBlock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));

            return receiveBlock;
        }

        public static async Task<ReceiveTransferBlock> ReceiveDaoFeeAsync(DagSystem sys, SendTransferBlock sendBlock)
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

                // dao     
                SellerCollateralPercentage = ((IDao)lastblock).SellerCollateralPercentage,
                ByerCollateralPercentage = ((IDao)lastblock).ByerCollateralPercentage,
                Treasure = ((IDao)lastblock).Treasure.ToDecimalDict().ToLongDict(),
                Description = ((IDao)lastblock).Description,
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

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

            receiveBlock.InitializeBlock(lastblock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));

            return receiveBlock;
        }

        public static async Task<ReceiveTransferBlock> ReceiveTradeFeeAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            // check exists
            var recv = await sys.Storage.FindBlockBySourceHashAsync(sendBlock.Hash);
            if (recv != null)
                return null;

            var txInfo = sendBlock.GetBalanceChanges(await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock);
            var lastblock = await sys.Storage.FindLatestBlockAsync(sendBlock.DestinationAccountId) as TransactionBlock;

            var lsb = await sys.Storage.GetLastServiceBlockAsync();

            var receiveBlock = new OtcCryptoTradeRecvBlock
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
                Trade = ((IOtcCryptoTrade)lastblock).Trade,
            };

            receiveBlock.AddTag(Block.MANAGEDTAG, "");   // value is always ignored

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

            receiveBlock.InitializeBlock(lastblock, (hash) => Signatures.GetSignature(sys.PosWallet.PrivateKey, hash, sys.PosWallet.AccountId));

            return receiveBlock;
        }

        public static Task<ReceiveTransferBlock> ReceiveNoneAsync(DagSystem sys, SendTransferBlock sendBlock)
        {
            return Task.FromResult<ReceiveTransferBlock>(null);
        }
    }
}
