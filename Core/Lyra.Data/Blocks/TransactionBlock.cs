using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;
using Newtonsoft.Json;

namespace Lyra.Core.Blocks
{
    // This transaction recording, in any direction (send or receive)
    public class TransactionInfo : IEquatable<TransactionInfo>
    {
        // This is the "pure" transacted amount (not including fee):
        public decimal Amount { get; set; }
        public string TokenCode { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as TransactionInfo);
        }

        public bool Equals(TransactionInfo other)
        {
            return other != null &&
                   Amount == other.Amount &&
                   TokenCode == other.TokenCode;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Amount, TokenCode);
        }

        public static bool operator ==(TransactionInfo left, TransactionInfo right)
        {
            return EqualityComparer<TransactionInfo>.Default.Equals(left, right);
        }

        public static bool operator !=(TransactionInfo left, TransactionInfo right)
        {
            return !(left == right);
        }
    }

    public class TransactionInfoEx: TransactionInfo
    {
        // TotalAmount = FeeAmount + Amount
        // This is the total amount of the account balance change including fee when applicable
        public decimal TotalBalanceChange { get; set; }

        public decimal FeeAmount { get; set; }

        public string FeeCode { get; set; }
    }

    public class BalanceChanges
    {
        public Dictionary<string, decimal> Changes { get; set; }
        public decimal FeeAmount { get; set; }

        public string FeeCode { get; set; }

        public BalanceChanges()
        {
            Changes = new Dictionary<string, decimal>();
        }

        public object OrderBy(Func<object, object> p)
        {
            throw new NotImplementedException();
        }
    }

    // this is base class for all send and receive blocks, i.e. all blocks containing transaction,
    // including genesis blocks and opening derivatives
    [BsonIgnoreExtraElements]
    public class TransactionBlock : Block
    {
        // this is the wallet address
        public string AccountID { get; set; }

        // this is the number of atomic units; it must be divided by the number of digits after the digital point for specific currency
        [BsonDictionaryOptions(DictionaryRepresentation.ArrayOfDocuments)]
        public Dictionary<string, long> Balances { get; set; }
        //public List<string, decimal> Balances { get; set; }

        public decimal Fee { get; set; } // the amount of transfer fee paid to the authorizers for processing transfer

        public string FeeCode { get; set; } // the fee currency code (multiple currencies can be accepted as a fee, which is defined by the latest service block)

        public AuthorizationFeeTypes FeeType { get; set; }

        // All nonfungible tokens (i.e. their original send block hashes) received on the account are stored here.
        // The total balances of all nonfungible token fo the same type are still reflected in Balances
        //public List<INonFungibleToken> NonFungibleTokens { get; set; }
        //public List<string> NonFungibleTokens { get; set; }

        // This is the non-fungible token being transacted.
        // It can be in either send or recive block.
        public NonFungibleToken NonFungibleToken { get; set; }

        ///// <summary>
        ///// When fee is zero and replaced by client-calculated proof of work
        ///// </summary>
        //public string PoW { get; set; }

        /// <summary>
        /// the account ID of target authorizer
        /// </summary>
        public string VoteFor { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData += AccountID + "|";
            extraData += BalanceToString() + "|";
            extraData += JsonConvert.SerializeObject(Fee) + "|";//Fee.ToString("0.############");
            extraData += FeeCode + "|";

            // TODO remove this once testnet reset
            if(Version < 3)
                extraData += ServiceHash + "|";
            extraData += FeeType.ToString() + "|";
            extraData += GetHashInputFromNonFungibleToken() + "|";
            extraData += VoteFor + "|";
            return extraData;
        }

        private string GetHashInputFromNonFungibleToken()
        {
            if (NonFungibleToken == null)
                return null;
            return NonFungibleToken.GetHashInput();
        }

        public virtual bool ValidateTransaction(TransactionBlock previousBlock)
        {
            if (Balances.Any(x => x.Value < 0))
                return false;

            var trs = GetBalanceChanges(previousBlock);

            if(this is TokenGenesisBlock)
            {
                // lyra down, other up
                if (trs.Changes.Count == 0 || trs.Changes[LyraGlobal.OFFICIALTICKERCODE] >= 0)
                    return false;
            }
            else
            {
                if (trs.Changes.Count == 0 || trs.Changes.Any(a => a.Value <= 0))
                    return false;
            }

            return true;
        }


        public bool ContainsNonFungibleToken()
        {
            return NonFungibleToken != null;
        }

        // This method compares this and previous blocks and returns the delta, which is the actual transaction represented by the block.
        // the trans amount is always positive, and it counts for the fee if transacting main currency, 
        // so the actual implementation will be different for send and receive blocks
        public virtual TransactionInfoEx GetTransaction(TransactionBlock previousBlock)
        {
            throw new NotImplementedException();
        }

        public virtual BalanceChanges GetBalanceChanges(TransactionBlock previousBlock)
        {
            throw new NotImplementedException();
        }


        // This method compares this and previous blocks and finds the non-fungible token being transacted (if any).
        // It actually returns the delta between two blocks, so the direction of the transaction is defined by the block type (send or receive).
        // Since non-fungible token info never changes, it is the same for send and receive blocks, but the finding logic is slightly different
        //public abstract INonFungibleToken GetNonFungibleTransaction(TransactionBlock previousBlock);

        public override string Print()
        {
            string result = base.Print();
            result += $"AccountID: {AccountID}\n";
            result += $"Balances: {BalanceToString()}\n";
            result += $"Fee: {JsonConvert.SerializeObject(Fee)}\n";
            result += $"FeeCode: {FeeCode}\n";
            result += $"FeeType: {FeeType.ToString()}\n";
            if (NonFungibleToken != null)
                result += $"NonFungibleToken: {NonFungibleToken.Print()}\n";
            else
                result += $"NonFungibleToken: {NonFungibleToken}\n";
            result += $"Voted Delegate/Authorizer: {VoteFor}\n";
            return result;
        }

        private string BalanceToString()
        {
            var sb = new StringBuilder();
            foreach(var kvp in Balances)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append($"{kvp.Key}:{kvp.Value}");
            }
            return sb.ToString();
        }

        protected string DictToStr<TKey, TValue>(Dictionary<TKey, TValue> dict)
        {
            var sb = new StringBuilder();
            foreach (var kvp in dict)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append($"{kvp.Key}:{kvp.Value}");
            }
            return sb.ToString();
        }
    }
}








