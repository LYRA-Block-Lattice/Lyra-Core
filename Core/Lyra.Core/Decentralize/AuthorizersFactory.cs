using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public class AuthorizersFactory
    {
        public AuthorizersFactory Singleton { get; private set; }
        static Dictionary<BlockTypes, string> _authorizers;
        static Dictionary<BlockTypes, IAuthorizer> _authorizerInstances;

        public AuthorizersFactory()
        {
            
        }

        public void Init()
        {
            if (_authorizerInstances != null)
                return;
                //throw new InvalidOperationException("Already initialized.");

            _authorizers = new Dictionary<BlockTypes, string>();
            _authorizers.Add(BlockTypes.SendTransfer, "SendTransferAuthorizer");
            
            _authorizers.Add(BlockTypes.ReceiveFee, "ReceiveTransferAuthorizer");
            //_authorizers.Add(BlockTypes.ReceiveNodeProfit, "ReceiveNodeProfitAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveFee, "NewAccountAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveTransfer, "NewAccountAuthorizer");
            //_authorizers.Add(BlockTypes.OpenAccountWithImport, "NewAccountWithImportAuthorizer");
            _authorizers.Add(BlockTypes.ReceiveTransfer, "ReceiveTransferAuthorizer");
            //_authorizers.Add(BlockTypes.ImportAccount, "ImportAccountAuthorizer");
            _authorizers.Add(BlockTypes.Consolidation, "ConsolidationBlockAuthorizer");
            _authorizers.Add(BlockTypes.Service, "ServiceAuthorizer");
            _authorizers.Add(BlockTypes.TradeOrder, "TradeOrderAuthorizer");
            _authorizers.Add(BlockTypes.Trade, "TradeAuthorizer");
            _authorizers.Add(BlockTypes.CancelTradeOrder, "CancelTradeOrderAuthorizer"); //HACK: wait for code check in
            _authorizers.Add(BlockTypes.ExecuteTradeOrder, "ExecuteTradeOrderAuthorizer");
            _authorizers.Add(BlockTypes.PoolFactory, "PoolFactoryAuthorizer");

            // pool, not needed
            //_authorizers.Add(BlockTypes.PoolGenesis, "PoolGenesisAuthorizer");
            //_authorizers.Add(BlockTypes.PoolDeposit, "PoolDepositAuthorizer");
            //_authorizers.Add(BlockTypes.PoolWithdraw, "PoolWithdrawAuthorizer");
            //_authorizers.Add(BlockTypes.PoolSwapIn, "PoolSwapInAuthorizer");
            //_authorizers.Add(BlockTypes.PoolSwapOut, "PoolSwapOutAuthorizer");

            //_authorizers.Add(BlockTypes.ProfitingGenesis, "ProfitingGenesisAuthorizer");
            //_authorizers.Add(BlockTypes.Profiting, "ProfitingAuthorizer");
            //_authorizers.Add(BlockTypes.Benefiting, "BenefitingAuthorizer");

            //_authorizers.Add(BlockTypes.StakingGenesis, "StakingGenesisAuthorizer");
            //_authorizers.Add(BlockTypes.Staking, "StakingAuthorizer");
            //_authorizers.Add(BlockTypes.UnStaking, "UnStakingAuthorizer");


            _authorizers.Add(BlockTypes.ReceiveAsFee, "ReceiveAsFeeAuthorizer");
            _authorizers.Add(BlockTypes.LyraTokenGenesis, "LyraGenesisAuthorizer");
            _authorizers.Add(BlockTypes.TokenGenesis, "TokenGenesisAuthorizer");

            // DEX
            //_authorizers.Add(BlockTypes.DexWalletGenesis, "DexWalletGenesisAuthorizer");
            //_authorizers.Add(BlockTypes.DexTokenMint, "DexTokenMintAuthorizer");
            //_authorizers.Add(BlockTypes.DexTokenBurn, "DexTokenBurnAuthorizer");
            //_authorizers.Add(BlockTypes.DexWithdrawToken, "DexWithdrawAuthorizer");
            //_authorizers.Add(BlockTypes.DexSendToken, "DexSendAuthorizer");
            //_authorizers.Add(BlockTypes.DexRecvToken, "DexReceiveAuthorizer");

            // DAO Note: not needed. dynamic work flow did this.
            //_authorizers.Add(BlockTypes.Orgnization, "DaoAuthorizer");
            //_authorizers.Add(BlockTypes.OrgnizationGenesis, "DaoGenesisAuthorizer");

            _authorizerInstances = new Dictionary<BlockTypes, IAuthorizer>();
            foreach(var kvp in _authorizers)
            {
                var authorizer = (IAuthorizer)Activator.CreateInstance(Type.GetType("Lyra.Core.Authorizers." + kvp.Value));
                _authorizerInstances.Add(kvp.Key, authorizer);
            }

            Singleton = this;
        }

        //public IAuthorizer this[BlockTypes blockType] => _authorizerInstances[blockType];

        public IAuthorizer Create(BlockTypes blockType)
        {
            return (IAuthorizer)Activator.CreateInstance(Type.GetType("Lyra.Core.Authorizers." + _authorizers[blockType]));
        }

        public void Register(BlockTypes blockType, string authrName)
        {
            _authorizers.Add(blockType, authrName);
        }
    }
}
