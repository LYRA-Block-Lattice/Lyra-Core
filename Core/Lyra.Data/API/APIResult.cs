using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Fees;
using Lyra.Data.API;
using Lyra.Data.API.WorkFlow;
using Lyra.Data.Blocks;
using Newtonsoft.Json;

namespace Lyra.Core.API
{
    public class APIResult : IEquatable<APIResult>
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

        public override bool Equals(object obj)
        {
            return Equals(obj as APIResult);
        }

        public bool Equals(APIResult other)
        {
            return other != null &&
                   GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ResultCode, ResultMessage);
        }

        public static bool operator ==(APIResult left, APIResult right)
        {
            return EqualityComparer<APIResult>.Default.Equals(left, right);
        }

        public static bool operator !=(APIResult left, APIResult right)
        {
            return !(left == right);
        }
    }

    public class SimpleJsonAPIResult : APIResult
    {
        public string JsonString { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), JsonString);
    }

    public class TransactionsAPIResult : APIResult
    {
        public List<TransactionDescription> Transactions { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = base.GetHashCode() + 19;

                if(null != Transactions)
                {
                    foreach (var t in Transactions)
                    {
                        hash = hash * 31 + (t == null ? 0 : t.GetHashCode());
                    }
                }

                return hash;
            }
        }
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

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Height, SyncHash, NetworkId);
    }

    // returns the authorization signatures for send or receive blocks
    public class AuthorizationAPIResult: APIResult
    {
        public string TxHash { get; set; }
        public override bool Equals(object obj)
        {
            return obj is AuthorizationAPIResult result &&
                   base.Equals(obj) &&
                   TxHash == result.TxHash;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), TxHash);
        }
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

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), TradeBlockData);
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

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), TradeBlockData);
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

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ListDataSerialized);
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

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ListDataSerialized);
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

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = base.GetHashCode() + 19;
                if(null != BlockDatas)
                foreach (var d in BlockDatas)
                {
                    hash = hash * 31 + (d == null ? 0 : d.GetHashCode());
                }
                if(null != ResultBlockTypes)
                foreach (var t in ResultBlockTypes)
                {
                    hash = hash * 31 + t.GetHashCode();
                }
                return hash;
            }
        }
    }

    // return the auhtorization signatures for send or receive blocks
    public class BlockAPIResult : APIResult
    {
        class MyBinder : Binder
        {
            public override MethodBase SelectMethod(BindingFlags bindingAttr, MethodBase[] match, Type[] types, ParameterModifier[] modifiers)
            {
                return match.First(m => m.IsGenericMethod);
            }

            #region not implemented
            public override MethodBase BindToMethod(BindingFlags bindingAttr, MethodBase[] match, ref object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] names, out object state) => throw new NotImplementedException();
            public override FieldInfo BindToField(BindingFlags bindingAttr, FieldInfo[] match, object value, CultureInfo culture) => throw new NotImplementedException();
            public override PropertyInfo SelectProperty(BindingFlags bindingAttr, PropertyInfo[] match, Type returnType, Type[] indexes, ParameterModifier[] modifiers) => throw new NotImplementedException();
            public override object ChangeType(object value, Type type, CultureInfo culture) => throw new NotImplementedException();
            public override void ReorderArgumentArray(ref object[] args, object state) => throw new NotImplementedException();
            #endregion
        }

        private static Dictionary<BlockTypes, MethodInfo> TypeDict = new Dictionary<BlockTypes, MethodInfo>();
        public static void Register(BlockTypes bt, Type type)
        {
            var methodInfo = typeof(JsonConvert).GetMethod("DeserializeObject",
                BindingFlags.Public | BindingFlags.Static,
                new MyBinder(),
                new[] { typeof(string) },
                null);

            var genericMethodInfo = methodInfo.MakeGenericMethod(type);
            TypeDict[bt] = genericMethodInfo;
        }

        public string BlockData { get; set; }
        public BlockTypes ResultBlockType { get; set; }

        public void SetBlock(Block block)
        {
            ResultBlockType = block.GetBlockType();
            BlockData = JsonConvert.SerializeObject(block);
        }

        public Block GetBlock()
        {
            Block block;

            if (TypeDict.ContainsKey(ResultBlockType))
                block = TypeDict[ResultBlockType].Invoke(null, new object[] { BlockData }) as Block;
            else
            {
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
                    case BlockTypes.ReceiveAsFee:
                        block = JsonConvert.DeserializeObject<ReceiveAsFeeBlock>(BlockData);
                        break;
                    case BlockTypes.OpenAccountWithReceiveTransfer:
                        block = JsonConvert.DeserializeObject<OpenWithReceiveTransferBlock>(BlockData);
                        break;
                    case BlockTypes.ReceiveAuthorizerFee:
                        block = JsonConvert.DeserializeObject<ReceiveAuthorizerFeeBlock>(BlockData);
                        break;
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
                    case BlockTypes.ImportAccount:
                        block = JsonConvert.DeserializeObject<ImportAccountBlock>(BlockData);
                        break;
                    case BlockTypes.OpenAccountWithImport:
                        block = JsonConvert.DeserializeObject<OpenAccountWithImportBlock>(BlockData);
                        break;
                    case BlockTypes.PoolFactory:
                        block = JsonConvert.DeserializeObject<PoolFactoryBlock>(BlockData);
                        break;
                    case BlockTypes.Null:
                        block = null;
                        break;
                    default:
                        throw new Exception($"Unknown block type: {ResultBlockType}");
                }
            }

            if(block is DaoRecvBlock dao && dao.Treasure == null)
            {
                Debugger.Break();
            }

            // here verify block signature. 
            if(block != null && block.VerifyHash())
            {
                return block;
            }
            else
            {
                Console.WriteLine($">>>>>\n{BlockData}\n>>>>");
                return null;
            }
                
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(base.GetHashCode(), BlockData, ResultBlockType);
        }
    }

    // returns the info about new transfer available for the account and calculated by the node
    //  (instead of returning the entire send block and its previous block and calculating at the client)
    public class NewTransferAPIResult: APIResult
    {
        public TransactionInfo Transfer { get; set; }
        public string SourceHash { get; set; }
        public NonFungibleToken NonFungibleToken { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Transfer, SourceHash, NonFungibleToken);
    }

    public class NewTransferAPIResult2 : APIResult
    {
        public BalanceChanges Transfer { get; set; }
        public string SourceHash { get; set; }
        public NonFungibleToken NonFungibleToken { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Transfer, SourceHash, NonFungibleToken);
    }

    public class NewFeesAPIResult : APIResult
    {
        public UnSettledFees pendingFees { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), pendingFees);
    }

    public class GetListStringAPIResult : APIResult
    {
        public List<string> Entities { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = base.GetHashCode() + 19;
                if(null != Entities)
                foreach (var t in Entities)
                {
                    hash = hash * 31 + (t == null ? 0 : t.GetHashCode());
                }
                return hash;
            }
        }
    }

    public class GetVersionAPIResult : APIResult
    {
        public int ApiVersion { get; set; }
        public string NodeVersion { get; set; }
        public bool UpgradeNeeded { get; set; }
        public bool MustUpgradeToConnect { get; set; }
        public string PosAccountId { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ApiVersion, NodeVersion, UpgradeNeeded, MustUpgradeToConnect);
    }

    public class GetSyncStateAPIResult : APIResult
    {
        public string NetworkID { get; set; }
        public string Signature { get; set; }   // sign public ip with private key
        public ConsensusWorkingMode SyncState { get; set; }
        public string LastConsolidationHash { get; set; }
        public NodeStatus Status { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), NetworkID, LastConsolidationHash, Status);
    }

    public class ExchangeAccountAPIResult : APIResult
    {
        public string AccountId { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), AccountId);
    }

    public class ExchangeBalanceAPIResult : ExchangeAccountAPIResult
    {
        public Dictionary<string, decimal> Balance { get; set; }
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = base.GetHashCode() + 19;
                if(null != Balance)
                foreach (var t in Balance)
                {
                    hash = hash * 31 + t.Key.GetHashCode() + t.Value.GetHashCode();
                }
                return hash;
            }
        }
    }

    public enum NotifySource { None, System, Balance, Dex, DShop, DPay };
    public class GetNotificationAPIResult : APIResult
    {
        public bool HasEvent { get; set; }
        public NotifySource Source { get; set; }
        public string Action { get; set; }
        public string Catalog { get; set; }
        public string ExtraInfo { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), HasEvent, Source, Action, Catalog, ExtraInfo);
    }

    public class PoolInfoAPIResult : BlockAPIResult
    {
        public string PoolFactoryAccountId { get; set; }
        public string PoolAccountId { get; set; }
        public string Token0 { get; set; }
        public string Token1 { get; set; }
        //public long SwapRito { get; set; }    // token0 / token1
    }
}
