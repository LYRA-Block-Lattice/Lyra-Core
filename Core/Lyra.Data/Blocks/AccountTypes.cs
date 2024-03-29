﻿namespace Lyra.Core.Blocks
{
    public enum AccountTypes : ushort
    {
        Standard = 1,
        Savings = 2,
        /// <summary>
        /// generic service type
        /// </summary>
        Service = 3,    // PBFT view
        PoolFactory = 5,
        Pool = 6,
        Staking = 7,
        Profiting = 8,
        DEX = 9,
        DAO = 10,
        OTC = 11,
        Voting = 12,
        /// <summary>
        /// bound to a physical server
        /// </summary>
        Server = 13,
        Guild = 14,

        // Universal Trades
        NFT = 30,
        Fiat,
        TOT,
        
        //NFTSell = 20,
        //NFTBuy,
        //TOTSell,
        //TOTBuy,
        //TokenSell,
        //TokenBuy,
        //OTCSell = 20,
        //OTCBuy,
        //SKUSell,    // goods
        //SKUBuy,
        //SVCSell,   // human labour, service product, like Fiver.com
        //SVCBuy,
    }
}
