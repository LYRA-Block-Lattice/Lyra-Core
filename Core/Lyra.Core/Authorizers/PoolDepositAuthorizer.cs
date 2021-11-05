using Lyra.Core.API;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public class PoolDepositAuthorizer : ReceiveTransferAuthorizer
    {
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is PoolDepositBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as PoolDepositBlock;

            if (block.SourceHash != block.RelatedTx)
                return APIResultCodes.InvalidRelatedTx;

            return await base.AuthorizeImplAsync(sys, tblock);
        }

        protected override async Task<APIResultCodes> VerifyBlockAsync(DagSystem sys, Block block, Block previousBlock)
        {
            // recalculate
            var recvBlock = block as PoolDepositBlock;
            var sendBlock = await sys.Storage.FindBlockByHashAsync((block as ReceiveTransferBlock).SourceHash) as SendTransferBlock;
            
            if(sendBlock == null)
            {
                // missing block. this node is lagged.
                return APIResultCodes.InvalidPreviousBlock;
            }
            
            var prevSend = await sys.Storage.FindBlockByHashAsync(sendBlock.PreviousHash) as TransactionBlock;
            var txInfo = sendBlock.GetBalanceChanges(prevSend);

            var latestPoolBlock = await sys.Storage.FindBlockByHashAsync(block.PreviousHash) as TransactionBlock;
            var poolGenesis = await sys.Storage.FindFirstBlockAsync(latestPoolBlock.AccountID) as PoolGenesisBlock;
            var depositBalance = new Dictionary<string, decimal>();
            var depositShares = new Dictionary<string, decimal>();
            if (latestPoolBlock.Balances.Any())
            {
                var lastBalance = latestPoolBlock.Balances.ToDecimalDict();
                var lastShares = ((IPool)latestPoolBlock).Shares.ToRitoDecimalDict();

                // the rito must be preserved for every deposition
                //var poolRito = lastBalance[poolGenesis.Token0] / lastBalance[poolGenesis.Token1];
                foreach (var oldBalance in lastBalance)
                {
                    depositBalance.Add(oldBalance.Key, oldBalance.Value + txInfo.Changes[oldBalance.Key]);
                }

                var prevBalance = lastBalance[poolGenesis.Token0];
                var curBalance = depositBalance[poolGenesis.Token0];

                foreach (var share in lastShares)
                {
                    depositShares.Add(share.Key, (share.Value * prevBalance / curBalance));
                }

                // merge share if any
                var r0 = txInfo.Changes[poolGenesis.Token0] / curBalance;

                if (depositShares.ContainsKey(sendBlock.AccountID))
                    depositShares[sendBlock.AccountID] += r0;
                else
                    depositShares.Add(sendBlock.AccountID, r0);
            }
            else
            {
                foreach (var token in txInfo.Changes)
                {
                    depositBalance.Add(token.Key, token.Value);
                }

                depositShares.Add(sendBlock.AccountID, 1m);   // 100%
            }

            if (!depositBalance.ToLongDict().OrderBy(a => a.Key)
                .SequenceEqual(recvBlock.Balances.OrderBy(a => a.Key)))
                return APIResultCodes.InvalidPoolDepositionAmount;

            if (!depositShares.ToRitoLongDict().OrderBy(a => a.Key)
                .SequenceEqual(recvBlock.Shares.OrderBy(a => a.Key)))
                return APIResultCodes.InvalidPoolDepositionRito;

            return await base.VerifyBlockAsync(sys, block, previousBlock);
        }
    }
}
