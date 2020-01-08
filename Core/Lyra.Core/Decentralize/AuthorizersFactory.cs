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
                throw new InvalidOperationException("Already initialized.");

            _authorizers = new Dictionary<BlockTypes, string>();
            _authorizers.Add(BlockTypes.SendTransfer, "SendTransferAuthorizer");
            _authorizers.Add(BlockTypes.LyraTokenGenesis, "GenesisAuthorizer");
            _authorizers.Add(BlockTypes.ReceiveFee, "ReceiveTransferAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveFee, "NewAccountAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithReceiveTransfer, "NewAccountAuthorizer");
            _authorizers.Add(BlockTypes.OpenAccountWithImport, "NewAccountWithImportAuthorizer");
            _authorizers.Add(BlockTypes.ReceiveTransfer, "ReceiveTransferAuthorizer");
            _authorizers.Add(BlockTypes.ImportAccount, "ImportAccountAuthorizer");
            _authorizers.Add(BlockTypes.TokenGenesis, "NewTokenAuthorizer");
            _authorizers.Add(BlockTypes.Consolidation, "ConsolidationBlockAuthorizer");

            _authorizerInstances = new Dictionary<BlockTypes, IAuthorizer>();
            foreach(var kvp in _authorizers)
            {
                var authorizer = (IAuthorizer)Activator.CreateInstance(Type.GetType("Lyra.Core.Authorizers." + kvp.Value));
                _authorizerInstances.Add(kvp.Key, authorizer);
            }

            Singleton = this;
        }

        public IAuthorizer this[BlockTypes blockType] => _authorizerInstances[blockType];
    }
}
