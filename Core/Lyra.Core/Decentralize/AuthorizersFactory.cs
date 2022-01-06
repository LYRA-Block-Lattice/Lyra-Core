using Lyra.Core.Authorizers;
using Lyra.Core.Blocks;
using Lyra.Data.Blocks;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public class AuthorizersFactory
    {
        public AuthorizersFactory Singleton { get; private set; }
        static Dictionary<BlockTypes, Type> _authorizers;
        static Dictionary<BlockTypes, IAuthorizer> _authorizerInstances;
        static TransactionAuthorizer _wfAuthorizer;

        public AuthorizersFactory()
        {
            
        }

        public void Init()
        {
            if (_authorizerInstances != null)
                return;
                //throw new InvalidOperationException("Already initialized.");

            _authorizers = new Dictionary<BlockTypes, Type>();
            _authorizers.Add(BlockTypes.SendTransfer, typeof(SendTransferAuthorizer));
            
            _authorizers.Add(BlockTypes.ReceiveFee, typeof(ReceiveTransferAuthorizer));
            //_authorizers.Add(BlockTypes.ReceiveNodeProfit, "ReceiveNodeProfitAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveFee, typeof(NewAccountAuthorizer));
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveTransfer, typeof(NewAccountAuthorizer));
            //_authorizers.Add(BlockTypes.OpenAccountWithImport, "NewAccountWithImportAuthorizer");
            _authorizers.Add(BlockTypes.ReceiveTransfer, typeof(ReceiveTransferAuthorizer));
            //_authorizers.Add(BlockTypes.ImportAccount, "ImportAccountAuthorizer");
            _authorizers.Add(BlockTypes.Consolidation, typeof(ConsolidationBlockAuthorizer));
            _authorizers.Add(BlockTypes.Service, typeof(ServiceAuthorizer));
            _authorizers.Add(BlockTypes.TradeOrder, typeof(TradeOrderAuthorizer));
            _authorizers.Add(BlockTypes.Trade, typeof(TradeAuthorizer));
            _authorizers.Add(BlockTypes.CancelTradeOrder, typeof(CancelTradeOrderAuthorizer)); //HACK: wait for code check in
            _authorizers.Add(BlockTypes.ExecuteTradeOrder, typeof(ExecuteTradeOrderAuthorizer));
            _authorizers.Add(BlockTypes.PoolFactory, typeof(PoolFactoryAuthorizer));

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


            _authorizers.Add(BlockTypes.ReceiveAsFee, typeof(ReceiveAsFeeAuthorizer));
            _authorizers.Add(BlockTypes.LyraTokenGenesis, typeof(LyraGenesisAuthorizer));
            _authorizers.Add(BlockTypes.TokenGenesis, typeof(TokenGenesisAuthorizer));

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
                var authorizer = (IAuthorizer)Activator.CreateInstance(kvp.Value);
                _authorizerInstances.Add(kvp.Key, authorizer);
            }

            _wfAuthorizer = new TransactionAuthorizer();

            Singleton = this;
        }

        public IAuthorizer Create(Block block)
        {
            //return (IAuthorizer)Activator.CreateInstance(_authorizers[blockType]);
            if (_authorizerInstances.ContainsKey(block.BlockType))
                return _authorizerInstances[block.BlockType];
            else if (block is IBrokerAccount || block is IPool)
                return _wfAuthorizer;
            else
                throw new Exception($"No authorizer for {block.BlockType}");
        }

        public void Register(BlockTypes blockType, Type authrType)
        {
            _authorizers.Add(blockType, authrType);

            var authorizer = (IAuthorizer)Activator.CreateInstance(authrType);
            _authorizerInstances.Add(blockType, authorizer);
        }
    }
}
