using System;
using System.Collections.Generic;
using System.Linq;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Fees;
using Lyra.Data.API;
using Newtonsoft.Json;

namespace Lyra.Core.API
{
    public class APIResult
    {
        public APIResultCodes ResultCode { get; set; }
        public string ResultMessage { get; set; }

        public APIResult()
        {
            ResultCode = APIResultCodes.UnknownError;
            //ResultMessage = string.Empty;
        }

        public bool Successful()
        {
            return ResultCode == APIResultCodes.Success;
        }
    }

    public class SimpleJsonAPIResult : APIResult
    {
        public string JsonString { get; set; }
    }

    public class TransactionsAPIResult : APIResult
    {
        public List<TransactionDescription> Transactions { get; set; }
    }

    public class AccountHeightAPIResult : APIResult
    {
        public long Height { get; set; }
        public string SyncHash { get; set; }
        public string NetworkId { get; set; }

        public AccountHeightAPIResult(): base()
        {
            Height = 0;
        }
    }

    // returns the authorization signatures for send or receive blocks
    public class AuthorizationAPIResult: APIResult
    {
        public string ServiceHash { get; set; }
        public List<AuthorizationSignature> Authorizations { get; set; }
    }

    public class TradeAPIResult : APIResult
    {
        public string TradeBlockData { get; set; }

        public void SetBlock(TradeBlock block)
        {
            TradeBlockData = JsonConvert.SerializeObject(block);
        }

        public TradeBlock GetBlock()
        {
           return JsonConvert.DeserializeObject<TradeBlock>(TradeBlockData);
        }
    }

    public class TradeOrderAuthorizationAPIResult : AuthorizationAPIResult
    {
        public string TradeBlockData { get; set; }

        public void SetBlock(TradeBlock block)
        {
            TradeBlockData = JsonConvert.SerializeObject(block);
        }

        public TradeBlock GetBlock()
        {
            return JsonConvert.DeserializeObject<TradeBlock>(TradeBlockData);
        }
    }

    public class ActiveTradeOrdersAPIResult : APIResult
    {
        public string ListDataSerialized { get; set; }

        public void SetList(List<TradeOrderBlock> list)
        {
            ListDataSerialized = JsonConvert.SerializeObject(list);
        }

        public List<TradeOrderBlock> GetList()
        {
            return JsonConvert.DeserializeObject<List<TradeOrderBlock>>(ListDataSerialized);
        }
    }


    public class NonFungibleListAPIResult : APIResult
    {
        public string ListDataSerialized { get; set; }

        public void SetList(List<NonFungibleToken> list)
        {
            ListDataSerialized = JsonConvert.SerializeObject(list);
        }

        public List<NonFungibleToken> GetList()
        {
            return JsonConvert.DeserializeObject<List<NonFungibleToken>>(ListDataSerialized);
        }
    }

    //// returns the auhtorization signatures for send or receive blocks
    //public class NewTransferAPIResult : APIResult
    //{
    //    public Block SendTransferBlock { get; set; }
    //    public Block TransactionBlock { get; set; }
    //}
    public class MultiBlockAPIResult : APIResult
    {
        public string[] BlockDatas { get; set; }
        public BlockTypes[] ResultBlockTypes { get; set; }

        public void SetBlocks(Block[] blocks)
        {
            BlockDatas = blocks.Select(a => JsonConvert.SerializeObject(a)).ToArray();
            ResultBlockTypes = blocks.Select(a => a.BlockType).ToArray();
        }

        public IEnumerable<Block> GetBlocks()
        {
            for(int i = 0; i < BlockDatas?.Length; i++)
            {
                var block = new BlockAPIResult { BlockData = BlockDatas[i], ResultBlockType = ResultBlockTypes[i] };
                yield return block.GetBlock();
            }
        }
    }

    // return the auhtorization signatures for send or receive blocks
    public class BlockAPIResult : APIResult
    {
        public string BlockData { get; set; }
        public BlockTypes ResultBlockType { get; set; }

        public void SetBlock(Block block)
        {
            BlockData = JsonConvert.SerializeObject(block);
        }

        public Block GetBlock()
        {
            Block block;
            switch (ResultBlockType)
            {
                case BlockTypes.SendTransfer:
                    block = JsonConvert.DeserializeObject<SendTransferBlock>(BlockData);
                    break;
                case BlockTypes.TokenGenesis:
                    block = JsonConvert.DeserializeObject<TokenGenesisBlock>(BlockData);
                    break;
                case BlockTypes.LyraTokenGenesis:
                    block = JsonConvert.DeserializeObject<LyraTokenGenesisBlock>(BlockData);
                    break;
                case BlockTypes.ReceiveTransfer:
                    block = JsonConvert.DeserializeObject<ReceiveTransferBlock>(BlockData);
                    break;
                case BlockTypes.OpenAccountWithReceiveTransfer:
                    block = JsonConvert.DeserializeObject<OpenWithReceiveTransferBlock>(BlockData);
                    break;
                case BlockTypes.ReceiveAuthorizerFee:
                    block = JsonConvert.DeserializeObject<ReceiveAuthorizerFeeBlock>(BlockData);
                    break;
                //case BlockTypes.ReceiveFee:
                //    block = JsonConvert.DeserializeObject<Blocks.Fees.ReceiveFeeBlock>(BlockData);
                //    break;
                //case BlockTypes.OpenAccountWithReceiveFee:
                //    block = JsonConvert.DeserializeObject<OpenWithReceiveFeeBlock>(BlockData);
                //    break;
                case BlockTypes.Service:
                    block = JsonConvert.DeserializeObject<ServiceBlock>(BlockData);
                    break;
                case BlockTypes.Consolidation:
                    block = JsonConvert.DeserializeObject<ConsolidationBlock>(BlockData);
                    break;
                case BlockTypes.TradeOrder:
                    block = JsonConvert.DeserializeObject<TradeOrderBlock>(BlockData);
                    break;
                case BlockTypes.CancelTradeOrder:
                    block = JsonConvert.DeserializeObject<CancelTradeOrderBlock>(BlockData);
                    break;
                case BlockTypes.Trade:
                    block = JsonConvert.DeserializeObject<TradeBlock>(BlockData);
                    break;
                case BlockTypes.ExecuteTradeOrder:
                    block = JsonConvert.DeserializeObject<ExecuteTradeOrderBlock>(BlockData);
                    break;
                case BlockTypes.ExchangingTransfer:
                    block = JsonConvert.DeserializeObject<ExchangingBlock>(BlockData);
                    break;
                case BlockTypes.ImportAccount:
                    block = JsonConvert.DeserializeObject<ImportAccountBlock>(BlockData);
                    break;
                case BlockTypes.OpenAccountWithImport:
                    block = JsonConvert.DeserializeObject<OpenAccountWithImportBlock>(BlockData);
                    break;
                case BlockTypes.NullTransaction:
                    block = JsonConvert.DeserializeObject<NullTransactionBlock>(BlockData);
                    break;
                case BlockTypes.Null:
                    block = null;
                    break;
                default:
                    throw new ApplicationException("Unknown block type");
            }
            // here verify block signature. 
            if(block != null && block.VerifyHash())
            {
                return block;
            }
            else
                return null;
        }
    }

    // returns the info about new transfer available for the account and calculated by the node
    //  (instead of returning the entire send block and its previous block and calculating at the client)
    public class NewTransferAPIResult: APIResult
    {
        public TransactionInfo Transfer { get; set; }
        public string SourceHash { get; set; }
        public NonFungibleToken NonFungibleToken { get; set; }
    }

    public class NewFeesAPIResult : APIResult
    {
        public UnSettledFees pendingFees { get; set; }
    }

    public class GetListStringAPIResult : APIResult
    {
        public List<string> Entities { get; set; }
    }

    public class GetVersionAPIResult : APIResult
    {
        public int ApiVersion { get; set; }
        public string NodeVersion { get; set; }
        public bool UpgradeNeeded { get; set; }
        public bool MustUpgradeToConnect { get; set; }        
    }

    public class GetSyncStateAPIResult : APIResult
    {
        public string NetworkID { get; set; }
        public string Signature { get; set; }   // sign public ip with private key
        public ConsensusWorkingMode SyncState { get; set; }
        public string LastConsolidationHash { get; set; }
        public NodeStatus Status { get; set; }
    }

    public class ExchangeAccountAPIResult : APIResult
    {
        public string AccountId { get; set; }
    }

    public class ExchangeBalanceAPIResult : ExchangeAccountAPIResult
    {
        public Dictionary<string, decimal> Balance { get; set; }
    }

    public enum NotifySource { None, System, Balance, Dex, DShop, DPay };
    public class GetNotificationAPIResult : APIResult
    {
        public bool HasEvent { get; set; }
        public NotifySource Source { get; set; }
        public string Action { get; set; }
        public string Catalog { get; set; }
        public string ExtraInfo { get; set; }
    }
}
