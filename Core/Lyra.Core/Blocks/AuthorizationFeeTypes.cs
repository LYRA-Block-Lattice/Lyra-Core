namespace Lyra.Core.Blocks
{
    public enum AuthorizationFeeTypes: ushort
    {
        /// <summary>
        /// No authorization fee is included in the block.
        /// The fee is either not required for this block or paid by the second party.
        /// </summary>
        NoFee = 0,

        /// <summary>
        /// The regualr fee is included in the block.
        /// The second party ether does not exist, or both parties of the transaction pay an equal amount of fee set by the network.
        /// </summary>
        Regular = 1,

        /// <summary>
        /// Doubled fee is included in the block.
        /// The second party of the transaction won't need to pay any fee. 
        /// </summary>
        BothParties = 2,

        /// <summary>
        /// Zero fee - the fee is "paid" by client's proof of work.
        /// </summary>
        PoW = 3
    }

}
