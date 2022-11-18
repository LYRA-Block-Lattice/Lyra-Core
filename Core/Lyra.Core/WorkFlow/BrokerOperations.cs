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
using Lyra.Core.Decentralize;

namespace Lyra.Core.WorkFlow
{
    public class BrokerOperations
    {
        public static Dictionary<BrokerRecvType, Func<DagSystem, SendTransferBlock, WorkflowAuthResult, Task<ReceiveTransferBlock>>> ReceiveViaCallback 
                => new Dictionary<BrokerRecvType, Func<DagSystem, SendTransferBlock, WorkflowAuthResult, Task<ReceiveTransferBlock>>>
                {
                    { BrokerRecvType.PFRecv, ReceivePoolFactoryFeeAsync },
                    { BrokerRecvType.DaoRecv, ReceiveDaoFeeAsync },
                    { BrokerRecvType.TradeRecv, ReceiveTradeFeeAsync },
                    { BrokerRecvType.None, ReceiveNoneAsync },
                };

        public static Dictionary<BrokerRecvType, Func<DagSystem, SendTransferBlock, WorkflowAuthResult, Task<SendTransferBlock>>> RefundViaCallback
                => new Dictionary<BrokerRecvType, Func<DagSystem, SendTransferBlock, WorkflowAuthResult, Task<SendTransferBlock>>>
                {
                    { BrokerRecvType.PFRecv, RefundPoolFactoryFeeAsync },
                    { BrokerRecvType.DaoRecv, RefundDaoFeeAsync },
                    { BrokerRecvType.TradeRecv, RefundTradeFeeAsync },
                    { BrokerRecvType.None, RefundNoneAsync },
                };

        public static async Task<SendTransferBlock> RefundPoolFactoryFeeAsync(DagSystem sys, SendTransferBlock sendBlock, WorkflowAuthResult authResult)
        {
            throw new NotImplementedException();
        }
        public static async Task<SendTransferBlock> RefundDaoFeeAsync(DagSystem sys, SendTransferBlock sendBlock, WorkflowAuthResult authResult)
        {
            throw new NotImplementedException();
        }
        public static async Task<SendTransferBlock> RefundTradeFeeAsync(DagSystem sys, SendTransferBlock sendBlock, WorkflowAuthResult authResult)
        {
            throw new NotImplementedException();
        }
        public static async Task<SendTransferBlock> RefundNoneAsync(DagSystem sys, SendTransferBlock sendBlock, WorkflowAuthResult authResult)
        {
            throw new NotImplementedException();
        }

        // every method must check if the operation has been done.
        // if has been done, return null.
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
                WFState.Received.ToString() : WFState.Refund.ToString());

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
                WFState.Received.ToString() : WFState.Refund.ToString());

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
                WFState.Received.ToString() : WFState.Refund.ToString());

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
    }
}
