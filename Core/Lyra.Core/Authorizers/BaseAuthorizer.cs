using Lyra.Core.Blocks;
using Lyra.Core.API;
using System;
using Lyra.Core.Utils;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Lyra.Core.Decentralize;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Linq;
using Neo;
using Akka.Actor;
using static Lyra.Core.Decentralize.ConsensusService;
using Lyra.Shared;
using Lyra.Data.API;
using Lyra.Data.Crypto;
using Lyra.Data.Utils;
using Lyra.Data.Blocks;

namespace Lyra.Core.Authorizers
{
    public delegate void AuthorizeCompleteEventHandler(object sender, AuthorizeCompletedEventArgs e);

    public class AuthorizeCompletedEventArgs : EventArgs
    {
        public Block Result { get; }
        public AuthorizeCompletedEventArgs(Block block)
        {
            Result = block;
        }
    }

    // -> Block
    public abstract class BaseAuthorizer : IAuthorizer
    {
        protected ILogger _log;
        public BaseAuthorizer()
        {
            _log = new SimpleLogger("BaseAuthorizer").Logger;
        }

        public async Task<(APIResultCodes, AuthorizationSignature)> AuthorizeAsync<T>(DagSystem sys, T tblock) where T : Block
        {
            var result = await AuthorizeImplAsync(sys, tblock);

            if (APIResultCodes.Success == result)
                return (APIResultCodes.Success, Sign(sys, tblock));
            else
                return (result, (AuthorizationSignature)null);
        }

        protected virtual async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock) where T : Block
        {
            var prevBlock = await sys.Storage.FindBlockByHashAsync(tblock.PreviousHash);
            var result = await StopWatcher.TrackAsync(() => VerifyWithPrevAsync(sys, tblock, prevBlock), "VerifyWithPrevAsync");
            return result;
        }

        protected virtual async Task<APIResultCodes> VerifyWithPrevAsync(DagSystem sys, Block block, Block previousBlock)
        {
            if (previousBlock != null)
            {
                var prevValid = StopWatcher.Track(() => block.IsBlockValid(previousBlock), "block.IsBlockValid");
                if (!prevValid)
                    return APIResultCodes.InvalidPreviousBlock;

                if (block.Height != previousBlock.Height + 1)
                    return APIResultCodes.InvalidBlockSequence;

                if(block.TimeStamp <=  previousBlock.TimeStamp)
                    return APIResultCodes.InvalidBlockTimeStamp;
            }
            else
            {
                if(block.Height != 1)
                    return APIResultCodes.InvalidBlockSequence;
            }

            // This is the double-spending check for send block!
            if (!string.IsNullOrEmpty(block.PreviousHash) && (await sys.Storage.FindBlockByPreviousBlockHashAsync(block.PreviousHash)) != null)
                return APIResultCodes.BlockWithThisPreviousHashAlreadyExists;

            if (block.Height <= 0)
                return APIResultCodes.InvalidIndexSequence;

            if (block.Height > 1 && previousBlock == null)       // bypass genesis block
                return APIResultCodes.PreviousBlockNotFound;

            if (block.Height == 1 && previousBlock != null)
                return APIResultCodes.InvalidIndexSequence;

            if (previousBlock != null && block.Height != previousBlock.Height + 1)
                return APIResultCodes.InvalidIndexSequence;

            return APIResultCodes.Success;
        }

        protected AuthorizationSignature Sign<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is Block))
                throw new System.Exception("APIResultCodes.InvalidBlockType");

            var block = tblock as Block;

            // sign with the authorizer key
            AuthorizationSignature authSignature = new AuthorizationSignature
            {
                Key = sys.PosWallet.AccountId,
                Signature = Signatures.GetSignature(sys.PosWallet.PrivateKey,
                    block.Hash, sys.PosWallet.AccountId)
            };

            return authSignature;
        }

        //protected async Task<bool> VerifyAuthorizationSignaturesAsync(TransactionBlock block)
        //{
        //    //block.ServiceHash = await sys.Storage.ServiceAccount.GetLatestBlock(block.ServiceHash);

        //    // TO DO - support multy nodes
        //    if (block.Authorizations == null || block.Authorizations.Count != 1)
        //        return false;

        //    if (block.Authorizations[0].Key != await sys.Storage.ServiceAccount.AccountId)
        //        return false;

        //    return Signatures.VerifyAuthorizerSignature(block.Hash + block.ServiceHash, block.Authorizations[0].Key, block.Authorizations[0].Signature);

        //}
    }
}
