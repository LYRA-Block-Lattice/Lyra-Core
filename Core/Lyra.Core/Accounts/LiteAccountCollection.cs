using System;
using System.Collections.Generic;
using Lyra.Core.Blocks;
using LiteDB;
using System.IO;
using Lyra.Core.Accounts;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Lyra.Core.Utils;

namespace Lyra.Core.Accounts
{
    // this is account collection (collection of block chains) used on the node side only
    // 
    public class LiteAccountCollection : IAccountCollection
    {
        private const string COLLECTION_DATABASE_NAME = "LyraBlockChain";

        private LiteDatabase _db = null;

        public LiteCollection<TransactionBlock> _blocks = null;

        private string FileName;

        ILogger _log;

        //<"account ID", "account blockchain">
        //Dictionary<string, AccountData> _collection = new Dictionary<string, AccountData>();

        public LiteAccountCollection(string Path)
        {
            _log = new SimpleLogger("LiteDB Ledge").Logger;

            FileName = Path + COLLECTION_DATABASE_NAME + ".db";
            string connectionString = "Filename=" + FileName;
            _db = new LiteDatabase(connectionString);
            _blocks = _db.GetCollection<TransactionBlock>("blocks");
            _blocks.EnsureIndex(x => x.AccountID);
            _blocks.EnsureIndex(x => x.Index);
            _blocks.EnsureIndex(x => x.UIndex);
            _blocks.EnsureIndex(x => x.BlockType);
            _blocks.EnsureIndex(x => x.Hash);
            _blocks.EnsureIndex(x => x.PreviousHash);
        }

        public void Delete()
        {
            //string fileName = COLLECTION_DATABASE_NAME + ".db";
            if (File.Exists(FileName))
                File.Delete(FileName);
        }

        public long GetBlockCount()
        {
            return (long)_blocks.LongCount();
        }

        public long GetBlockCount(string AccountId)
        {
            var count = _blocks.LongCount(Query.EQ("AccountID", AccountId));

            return count;
        }

        public bool AccountExists(string AccountId)
        {
            return GetBlockCount(AccountId) > 0;
        }

        public ServiceBlock GetLastServiceBlock()
        {
            var finds = _blocks.Find(Query.EQ("BlockType", "Service"))
                .OrderByDescending(b => b.UIndex)
                .First();
            return finds as ServiceBlock;
        }
        
        public ConsolidationBlock GetSyncBlock()
        {
            var finds = _blocks.Find(Query.EQ("BlockType", "Consolidation"))
                    .OrderByDescending(b => b.UIndex)
                    .First();
            return finds as ConsolidationBlock;
        }

        public TransactionBlock FindLatestBlock()
        {
            var ui = _blocks.Max("UIndex");
            var block = _blocks.FindOne(Query.EQ("UIndex", ui));
            
            return block;
        }

        public TransactionBlock FindLatestBlock(string AccountId)
        {
            var block = _blocks.Find(Query.EQ("AccountID", AccountId))
                .OrderByDescending(a => a.Index)
                .FirstOrDefault();

            return block;
        }

        public TokenGenesisBlock FindTokenGenesisBlock(string Hash, string Ticker)
        {
            //TokenGenesisBlock result = null;
            if (!string.IsNullOrEmpty(Hash))
            {
                var result = _blocks.FindOne(Query.EQ("Hash", Hash));
                if (result != null)
                    return result as TokenGenesisBlock;
            }

            // to do - try to replace this by indexed search using BlockType indexed field (since we can't index Ticker field):
            // find all GenesysBlocks first, then check if one of them has the right ticker
            if (!string.IsNullOrEmpty(Ticker))
            {
                var result = _blocks.FindOne(Query.EQ("Ticker", Ticker));
                if (result != null)
                    return result as TokenGenesisBlock;
            }

            return null;
        }

        public List<TokenGenesisBlock> FindTokenGenesisBlocks(string keyword)
        {
            var result = _blocks.Find(Query.EQ("BlockType", "TokenGenesisBlock"));
            var genBlocks = result.Cast<TokenGenesisBlock>();

            if (string.IsNullOrEmpty(keyword))
            {
                return genBlocks.ToList();
            }
            else
            {
                return genBlocks.Where(a => a.Ticker.Contains(keyword)).ToList();
            }
        }

        public NullTransactionBlock FindNullTransBlockByHash(string hash)
        {
            var finds = _blocks.Find(a => a.BlockType == BlockTypes.NullTransaction && (a as NullTransactionBlock).FailedBlockHash == hash)
                    .FirstOrDefault();
            return finds as NullTransactionBlock;
        }

        public TransactionBlock FindBlockByHash(string hash)
        {
            var result = _blocks.FindOne(Query.EQ("Hash", hash));
            return result;
        }

        public TransactionBlock FindBlockByHash(string AccountId, string hash)
        {
            var result = _blocks.FindOne(Query.And(Query.EQ("AccountID", AccountId), Query.EQ("Hash", hash)));
            return result;
        }

        public List<NonFungibleToken> GetNonFungibleTokens(string AccountId)
        {

            BsonArray p1 = new BsonArray();
            p1.Add(BlockTypes.ReceiveTransfer.ToString());
            p1.Add(BlockTypes.OpenAccountWithReceiveTransfer.ToString());
            p1.Add(BlockTypes.OpenAccountWithImport.ToString());

            var allNonFungibleReceiveBlocks = _blocks.Find(
                Query.And(Query.In("BlockType", p1),
                Query.And(Query.EQ("AccountID", AccountId), Query.Not("NonFungibleToken", null))));

            // TO DO - support the scenario when the owner resends to the token to another account.

            //BsonArray p2 = new BsonArray();
            //p2.Add(BlockTypes.SendTransfer.ToString());
            //p2.Add(BlockTypes.ExecuteTradeOrder.ToString());
            //p2.Add(BlockTypes.Trade.ToString());

            //var allNonFungibleSendBlocks = _blocks.Find(
            //    Query.And(Query.In("BlockType", p2),
            //    Query.And(Query.EQ("AccountID", AccountId), Query.Not("NonFungibleToken", null))));

            var the_list = new List<NonFungibleToken>();

            foreach (TransactionBlock receiveBlock in allNonFungibleReceiveBlocks)
            {
                the_list.Add(receiveBlock.NonFungibleToken);
                //foreach (TransactionBlock sendBlock in allNonFungibleSendBlocks)
                //{
                //    if (sendBlock.NonFungibleToken.OriginHash == receiveBlock.NonFungibleToken.OriginHash &&
                //        sendBlock.Index > receiveBlock.Index) // the send should occure after the receive, otherwise it was sent to itself
                //    {
                //        the_list.Remove(receiveBlock.NonFungibleToken);
                //        break;
                //    }
                //}
            }
            if (the_list.Count > 0)
                return the_list;

            return null;
        }

        public TransactionBlock FindBlockByPreviousBlockHash(string previousBlockHash)
        {
            var result = _blocks.FindOne(Query.EQ("PreviousHash", previousBlockHash));
            return result;
        }

        public ReceiveTransferBlock FindBlockBySourceHash(string hash)
        {
            var result = _blocks.Find(Query.EQ("SourceHash", hash));

            foreach (var block in result)
            {
                if (block.BlockType == BlockTypes.OpenAccountWithReceiveFee || block.BlockType == BlockTypes.ReceiveFee)
                    continue;
                else
                    return block as ReceiveTransferBlock;
            }
            return null;
        }

        public TransactionBlock FindBlockByIndex(string AccountId, long index)
        {
            var block = _blocks.FindOne(Query.And(Query.EQ("AccountID", AccountId), Query.EQ("Index", index)));
            return block;
        }

        public SendTransferBlock FindUnsettledSendBlock(string AccountId)
        {
            // First, let find all send blocks:
            // (It can be optimzed as it's going to be growing, so it can be called with munimum Service Chain Height parameter to look only for recent blocks) 
            var allSendBlocks = _blocks.Find(Query.EQ("DestinationAccountId", AccountId));
            //if (allSendBlocks == null)
            //  return null;
            foreach (SendTransferBlock sendBlock in allSendBlocks)
            {
                // Now, let's try to fetch the corresponding receive block:

                BsonArray p = new BsonArray();
                p.Add(BlockTypes.ReceiveTransfer.ToString());
                p.Add(BlockTypes.OpenAccountWithReceiveTransfer.ToString());
                p.Add(BlockTypes.OpenAccountWithImport.ToString());
                p.Add(BlockTypes.ImportAccount.ToString());

                var thisAccountReceiveBlock = _blocks.FindOne(
                    Query.And(Query.In("BlockType", p), Query.And(Query.EQ("AccountID", AccountId), Query.EQ("SourceHash", sendBlock.Hash))));

                if (thisAccountReceiveBlock == null)
                    return sendBlock;

                // If the receive block does not exists, this send block is our guy:
                //var any_receive_block_with_this_source = FindBlockBySourceHash(sendBlock.Hash);

                //if (any_receive_block_with_this_source == null)
                //    return sendBlock;

            }
            return null;
        }

        /// <summary>
        /// Returns the first unexecuted trade aimed to an order created on the account.
        /// </summary>
        /// <param name="AccountId"></param>
        /// <param name="BuyTokenCode">
        /// The code of the token being purchased (optional).
        /// </param>
        /// <param name="SellTokenCode">
        /// The code of the token being sold (optional).
        /// </param>
        /// <returns></returns>
        public TradeBlock FindUnexecutedTrade(string AccountId, string BuyTokenCode, string SellTokenCode)
        {
            if (BuyTokenCode == "*")
                BuyTokenCode = null;

            if (SellTokenCode == "*")
                SellTokenCode = null;

            // First, let find all the trade blocks aimed to this account:
            var trades = _blocks.Find(Query.And(Query.EQ("BlockType", BlockTypes.Trade.ToString()), Query.EQ("DestinationAccountId", AccountId)));

            foreach (TradeBlock trade in trades)
            {
                var trade_execution = _blocks.FindOne(Query.And(Query.EQ("BlockType", BlockTypes.ExecuteTradeOrder.ToString()), Query.EQ("TradeId", trade.Hash)));
                if (trade_execution != null)
                    continue;

                var trade_cancellation = _blocks.FindOne(Query.And(Query.EQ("BlockType", BlockTypes.CancelTradeOrder.ToString()), Query.EQ("TradeOrderId", trade.TradeOrderId)));
                if (trade_cancellation != null)
                    continue;

                if (!string.IsNullOrEmpty(BuyTokenCode) && BuyTokenCode != trade.BuyTokenCode)
                    continue;

                if (!string.IsNullOrEmpty(SellTokenCode) && SellTokenCode != trade.SellTokenCode)
                    continue;

                return trade;
            }
            return null;
        }

        public List<TradeOrderBlock> GetTradeOrderBlocks()
        {
            var list = new List<TradeOrderBlock>();
            //var blocks = _blocks.Find(Query.And(Query.EQ("BlockType", BlockTypes.TradeOrder.ToString()), Query.EQ("OrderType", order_type.ToString())));
            var blocks = _blocks.Find(Query.EQ("BlockType", BlockTypes.TradeOrder.ToString()));

            if (blocks != null)
                foreach (TradeOrderBlock block in blocks)
                    list.Add(block);

            return list;
        }

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        public List<string> GetTradeOrderCancellations()
        {
            var list = new List<string>();
            var blocks = _blocks.Find(Query.EQ("BlockType", BlockTypes.CancelTradeOrder.ToString()));

            if (blocks != null)
                foreach (CancelTradeOrderBlock block in blocks)
                    list.Add(block.TradeOrderId);

            return list;
        }

        // returns the list of hashes (order IDs) of all cancelled trade order blocks
        public List<string> GetExecutedTradeOrderBlocks()
        {
            var list = new List<string>();
            var blocks = _blocks.Find(Query.EQ("BlockType", BlockTypes.ExecuteTradeOrder.ToString()));

            if (blocks != null)
                foreach (ExecuteTradeOrderBlock block in blocks)
                    list.Add(block.TradeOrderId);

            return list;
        }

        public bool AddBlock(TransactionBlock block)
        {
            if (block.Index == 0 || block.UIndex == 0)
            {
                _log.LogWarning("AccountCollection=>AddBlock: Block with zero index/UIndex is now allowed!");
                return false;
            }

            if (GetBlockByUIndex(block.UIndex) != null)
            {
                _log.LogWarning("AccountCollection=>AddBlock: Block with such UIndex already exists!");
                return false;
            }

            if (FindBlockByHash(block.Hash) != null)
            {
                _log.LogWarning("AccountCollection=>AddBlock: Block with such Hash already exists!");
                return false;
            }

            if (FindBlockByIndex(block.AccountID, block.Index) != null)
            {
                _log.LogWarning("AccountCollection=>AddBlock: Block with such Index already exists!");
                return false;
            }

            _blocks.Insert(block);

            return true;
        }

        public void Dispose()
        {
            if (_db != null)
                _db.Dispose();
        }

        public long GetNewestBlockUIndex()
        {
            var result = FindLatestBlock();
            if (result == null)
                return 0;
            return result.UIndex;
        }

        public TransactionBlock GetBlockByUIndex(long uindex)
        {
            var block = _blocks.Find(Query.EQ("UIndex", uindex)).FirstOrDefault();
            return block;
        }

    }
}