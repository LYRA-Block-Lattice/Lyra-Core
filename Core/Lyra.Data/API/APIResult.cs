using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Humanizer;
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

        string _errmsg;
        public string ResultMessage 
        {
            get
            {
                return string.IsNullOrEmpty(_errmsg) ? ResultCode.Humanize() : _errmsg;
            }
            set
            {
                _errmsg = value;
            }
        }

        public APIResult()
        {
            ResultCode = APIResultCodes.UndefinedError;
            _errmsg = string.Empty;
        }

        public static APIResult Success => new APIResult { ResultCode = APIResultCodes.Success };

        public bool Successful()
        {
            return ResultCode == APIResultCodes.Success;
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as APIResult);
        }

        public bool Equals(APIResult? other)
        {
            if (other is null)
                return false;

            return GetHashCode() == other.GetHashCode();
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

        public override string ToString()
        {
            return ResultMessage;
        }
    }

    public class SimpleJsonAPIResult : APIResult
    {
        public string JsonString { get; set; } = null!;

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), JsonString);

        public static SimpleJsonAPIResult Create(object o)
        {
            return new SimpleJsonAPIResult
            {
                ResultCode = APIResultCodes.Success,
                JsonString = JsonConvert.SerializeObject(o),
            };
        }

        public T? Deserialize<T>()
        {
            return JsonConvert.DeserializeObject<T>(JsonString);
        }
    }

    public class TransactionsAPIResult : APIResult
    {
        public List<TransactionDescription>? Transactions { get; set; }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = base.GetHashCode() + 19;

                if(null != Transactions)
                {
                    foreach (var t in Transactions)
                    {
                        hash = hash * 31 + t.GetHashCode();
                    }
                }

                return hash;
            }
        }
    }

    public class AccountHeightAPIResult : APIResult
    {
        public long Height { get; set; }
        public string SyncHash { get; set; } = null!;
        public string NetworkId { get; set; } = null!;

        public AccountHeightAPIResult(): base()
        {
            Height = 0;
        }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Height, SyncHash, NetworkId);
    }

    // returns the authorization signatures for send or receive blocks
    public class AuthorizationAPIResult: APIResult
    {
        public string TxHash { get; set; } = null!;
        public override bool Equals(object? obj)
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
        public string TradeBlockData { get; set; } = null!;

        public void SetBlock(TradeBlock block)
        {
            TradeBlockData = JsonConvert.SerializeObject(block);
        }

        public TradeBlock? GetBlock()
        {
           return JsonConvert.DeserializeObject<TradeBlock>(TradeBlockData);
        }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), TradeBlockData);
    }

    public class TradeOrderAuthorizationAPIResult : AuthorizationAPIResult
    {
        public string TradeBlockData { get; set; } = null!;

        public void SetBlock(TradeBlock block)
        {
            TradeBlockData = JsonConvert.SerializeObject(block);
        }

        public TradeBlock? GetBlock()
        {
            return JsonConvert.DeserializeObject<TradeBlock>(TradeBlockData);
        }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), TradeBlockData);
    }

    public class ActiveTradeOrdersAPIResult : APIResult
    {
        public string ListDataSerialized { get; set; } = null!;

        public void SetList(List<TradeOrderBlock> list)
        {
            ListDataSerialized = JsonConvert.SerializeObject(list);
        }

        public List<TradeOrderBlock>? GetList()
        {
            return JsonConvert.DeserializeObject<List<TradeOrderBlock>>(ListDataSerialized);
        }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ListDataSerialized);
    }


    public class NonFungibleListAPIResult : APIResult
    {
        public string ListDataSerialized { get; set; } = null!;

        public void SetList(List<NonFungibleToken> list)
        {
            ListDataSerialized = JsonConvert.SerializeObject(list);
        }

        public List<NonFungibleToken>? GetList()
        {
            return JsonConvert.DeserializeObject<List<NonFungibleToken>>(ListDataSerialized);
        }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ListDataSerialized);
    }

    public class ContainerAPIResult : APIResult
    {
        public Dictionary<string, MultiBlockAPIResult> Container { get; set; }

        public ContainerAPIResult()
        {
            Container = new Dictionary<string, MultiBlockAPIResult>();
        }
        public void AddBlocks(string name, Block[] blocks)
        {
            Container.Add(name, new MultiBlockAPIResult(blocks));
        }

        public IEnumerable<Block?> GetBlocks(string name)
        {
            if(Container.ContainsKey(name))
            {
                var BlockDatas = Container[name];
                return BlockDatas.GetBlocks();
            }
            else
                return Enumerable.Empty<Block>();
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = base.GetHashCode() + 19;
                if(null != Container)
                {
                    foreach(var kvp in Container)
                    {
                        hash = hash * 31 + (kvp.Key == null ? 0 : kvp.Key.GetHashCode());
                        hash = hash * 31 + kvp.Value.GetHashCode();
                    }
                }
                return hash;
            }
        }
    }

    public class MultiBlockAPIResult : APIResult
    {
        public string[] BlockDatas { get; set; } = null!;
        public BlockTypes[] ResultBlockTypes { get; set; } = null!;

        public MultiBlockAPIResult()
        {
        }

        public MultiBlockAPIResult(Block[] blocks)
        {
            SetBlocks(blocks);
        }

        public void SetBlocks(Block[] blocks)
        {
            BlockDatas = blocks.Select(a => JsonConvert.SerializeObject(a)).ToArray();
            ResultBlockTypes = blocks.Select(a => a.BlockType).ToArray();
        }

        public IEnumerable<Block?> GetBlocks()
        {
            for(int i = 0; i < BlockDatas?.Length; i++)
            {
                var block = new BlockAPIResult { BlockData = BlockDatas[i], ResultBlockType = ResultBlockTypes[i] };
                yield return block.GetBlock();
            }
        }

        public IEnumerable<T?> GetBlocks<T>() where T : Block
        {
            for (int i = 0; i < BlockDatas?.Length; i++)
            {
                var block = new BlockAPIResult { BlockData = BlockDatas[i], ResultBlockType = ResultBlockTypes[i] };
                yield return block.GetBlock() as T;
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
        public class MyBinder : Binder
        {
            public override MethodBase? SelectMethod(BindingFlags bindingAttr, MethodBase[] match, Type[] types, ParameterModifier[]? modifiers)
            {
                return match.First(m => m.IsGenericMethod);
            }

            #region not implemented
            public override MethodBase BindToMethod(BindingFlags bindingAttr, MethodBase[] match, ref object?[] args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? names, out object? state) => throw new NotImplementedException();
            public override FieldInfo BindToField(BindingFlags bindingAttr, FieldInfo[] match, object value, CultureInfo? culture) => throw new NotImplementedException();
            public override PropertyInfo SelectProperty(BindingFlags bindingAttr, PropertyInfo[] match, Type? returnType, Type[]? indexes, ParameterModifier[]? modifiers) => throw new NotImplementedException();
            public override object ChangeType(object value, Type type, CultureInfo? culture) => throw new NotImplementedException();
            public override void ReorderArgumentArray(ref object?[] args, object state) => throw new NotImplementedException();
            #endregion
        }

        private static Dictionary<BlockTypes, MethodInfo> TypeDict = new Dictionary<BlockTypes, MethodInfo>();
        static void Register(BlockTypes bt, Type type)
        {
            var methodInfo = typeof(JsonConvert).GetMethod("DeserializeObject",
                BindingFlags.Public | BindingFlags.Static,
                new MyBinder(),
                new[] { typeof(string) },
                null);

            if(methodInfo != null)
            {
                var genericMethodInfo = methodInfo.MakeGenericMethod(type);
                TypeDict[bt] = genericMethodInfo;
            }

            //File.AppendAllText(@"c:\tmp\hash.txt", $"{bt} {genericMethodInfo}\n");
        }

        public string BlockData { get; set; } = null!;
        public BlockTypes ResultBlockType { get; set; }

        private static Block? Create(Type t)
        {
            try
            {
                return Activator.CreateInstance(t) as Block;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"In BlockAPIResult: {ex}");
                return null;
            }
        }

        static BlockAPIResult()
        {
            var exporters = typeof(Block)
                .Assembly.GetTypes()
                .Where(t => t.IsSubclassOf(typeof(Block)) && !t.IsAbstract)
                .Select(t => new
                {
                    b = Create(t),
                    t
                });
            foreach(var entry in exporters)
            {
                if(entry.b != null)
                {
                    BlockTypes bt;
                    try
                    {
                        MethodInfo? dynMethod = entry.t.GetMethod("GetBlockType",
                            BindingFlags.NonPublic | BindingFlags.Instance);

                        if(dynMethod != null)
                        {
                            var x = dynMethod.Invoke(entry.b, new object[] { });
                            if(x != null)
                            {
                                bt = (BlockTypes)x;
                                //Console.WriteLine($"{bt}: {entry.t.Name}");
                                if (bt != BlockTypes.Null)
                                    Register(bt, entry.t);
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        public static BlockAPIResult Create(Block block)
        {
            var result = new BlockAPIResult();
            result.SetBlock(block);
            result.ResultCode = APIResultCodes.Success;
            return result;
        }

        public void SetBlock(Block block)
        {
            ResultBlockType = block.BlockType;
            BlockData = JsonConvert.SerializeObject(block);
        }

        public T? As<T>() where T : class
        {
            return Successful() ? GetBlock() as T : null;
        }

        public Block? GetBlock()
        {
            Block? block = null;

            if (TypeDict.ContainsKey(ResultBlockType))
                block = TypeDict[ResultBlockType].Invoke(null, new object[] { BlockData }) as Block;

            // here verify block signature. 
            if(block != null && block.VerifyHash())
            {
                return block;
            }
            else
            {
                //File.AppendAllText(@"c:\tmp\hash.txt", $"Block {block.Hash} New txt: {block.GetHashInput()}\n");
                //Console.WriteLine($"hash input: \n{block.GetHashInput()}");
                //Console.WriteLine($">>>>>Block Hash Verification Error\n{BlockData}\n>>>>");
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
        public TransactionInfo? Transfer { get; set; }
        public string? SourceHash { get; set; }
        public NonFungibleToken? NonFungibleToken { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Transfer, SourceHash, NonFungibleToken);
    }

    public class NewTransferAPIResult2 : APIResult
    {
        public BalanceChanges? Transfer { get; set; }
        public string? SourceHash { get; set; }
        public NonFungibleToken? NonFungibleToken { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), Transfer, SourceHash, NonFungibleToken);
    }

    public class NewFeesAPIResult : APIResult
    {
        public UnSettledFees? pendingFees { get; set; }

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), pendingFees);
    }

    public class GetListStringAPIResult : APIResult
    {
        public List<string>? Entities { get; set; }

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
        public string NodeVersion { get; set; } = null!;
        public bool UpgradeNeeded { get; set; }
        public bool MustUpgradeToConnect { get; set; }
        public string PosAccountId { get; set; } = null!;

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), ApiVersion, NodeVersion, UpgradeNeeded, MustUpgradeToConnect);
    }

    public class GetSyncStateAPIResult : APIResult
    {
        public string NetworkID { get; set; } = null!;
        public string Signature { get; set; } = null!; // sign public ip with private key
        public ConsensusWorkingMode SyncState { get; set; }
        public string LastConsolidationHash { get; set; } = null!;
        public NodeStatus Status { get; set; } = null!;

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), NetworkID, LastConsolidationHash, Status);
    }

    public class ExchangeAccountAPIResult : APIResult
    {
        public string AccountId { get; set; } = null!;

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), AccountId);
    }

    public class ExchangeBalanceAPIResult : ExchangeAccountAPIResult
    {
        public Dictionary<string, decimal> Balance { get; set; } = null!;
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
        public string Action { get; set; } = null!;
        public string Catalog { get; set; } = null!;
        public string ExtraInfo { get; set; } = null!;

        public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), HasEvent, Source, Action, Catalog, ExtraInfo);
    }

    public class PoolInfoAPIResult : BlockAPIResult
    {
        public string PoolFactoryAccountId { get; set; } = null!;
        public string? PoolAccountId { get; set; }
        public string? Token0 { get; set; }
        public string? Token1 { get; set; }
        //public long SwapRito { get; set; }    // token0 / token1
    }

    public class ImageUploadResult : APIResult
    {
        public string Hash { get; set; } = null!;
        public string Url { get; set; } = null!;
    }
}
