
using Lyra.Core.API;
using MongoDB.Bson.Serialization.Attributes;

namespace Lyra.Core.Blocks
{
    [BsonIgnoreExtraElements]
    public class ReceiveTransferBlock : TransactionBlock
    {

        // Hash of the send block
        public string SourceHash { get; set; }

        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + SourceHash + "|";
            return extraData;
        }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.ReceiveTransfer;
        }

        public override TransactionInfoEx GetTransaction(TransactionBlock previousBlock)
        {
            var transaction = new TransactionInfoEx() { TokenCode = LyraGlobal.LYRATICKERCODE, Amount = 0, FeeAmount = 0, FeeCode = null };

            // let's find te balance that was changed since the previous block - to determine the token being transacted
            foreach (var balance in this.Balances)
                if (previousBlock != null)
                {
                    if (!previousBlock.Balances.ContainsKey(balance.Key) || previousBlock.Balances[balance.Key] != balance.Value)
                    {
                        transaction.TokenCode = balance.Key;

                        if (!previousBlock.Balances.ContainsKey(balance.Key))
                            transaction.Amount = this.Balances[balance.Key];
                        else
                            transaction.Amount = this.Balances[balance.Key] - previousBlock.Balances[balance.Key];

                        break;
                    }
                }
                else
                {
                    transaction.TokenCode = balance.Key;
                    transaction.Amount = this.Balances[balance.Key];
                    break;
                }

            //// if no token is being transfered, it's default token (like LYR ot LGT depending on configuration) itself
            //if (transaction.Token == LyraGlobal.LYRA_TICKER_CODE)
            //transaction.Amount = this.Balances[LyraGlobal.LYRA_TICKER_CODE] - previousBlock.Balances[LyraGlobal.LYRA_TICKER_CODE];
            transaction.TotalBalanceChange = transaction.Amount;

            return transaction;
        }

        //public override INonFungibleToken GetNonFungibleTransaction(TransactionBlock previousBlock)
        //{


        //    // let's simply find the first token that is not present in the previous block
        //    foreach (var token in this.NonFungibleTokens)
        //    {
        //        if (previousBlock.NonFungibleTokens != null)
        //        {
        //            bool found = false;
        //            foreach (var previous_token in previousBlock.NonFungibleTokens)
        //            {
        //                if (token.TokenCode == previous_token.TokenCode && token.SerialNumber == previous_token.SerialNumber)
        //                {
        //                    found = true;
        //                    break;
        //                }
        //            }
        //            if (!found)
        //                return token;
        //        }
        //        else
        //        {
        //            return token;
        //        }
        //    }
        //    return null;

        //}

        public override string Print()
        {
            string result = base.Print();
            result += $"SourceHash: {SourceHash}\n";
            return result;
        }

        //public override bool ValidateTransaction(TransactionBlock previousBlock)
        //{
        //    if (!base.ValidateTransaction(previousBlock))
        //        return false;

        //    var trs = CalculateTransaction(previousBlock);

        //    if (trs.Amount != Transaction.Amount)
        //        return false;

        //    return true;
        //}



    }

    [BsonIgnoreExtraElements]
    public class OpenWithReceiveTransferBlock : ReceiveTransferBlock, IOpeningBlock
    {
        public AccountTypes AccountType { get; set; }

        public override BlockTypes GetBlockType()
        {
            return BlockTypes.OpenAccountWithReceiveTransfer;
        }


        protected override string GetExtraData()
        {
            string extraData = base.GetExtraData();
            extraData = extraData + AccountType + "|";
            return extraData;
        }

    }
}
