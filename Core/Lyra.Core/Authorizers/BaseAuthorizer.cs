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
        ILogger _log;
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

            var result = await VerifyBlockAsync(sys, tblock, prevBlock);
            return result;
        }

        protected virtual async Task<APIResultCodes> VerifyBlockAsync(DagSystem sys, Block block, Block previousBlock)
        {
            if (previousBlock != null && !block.IsBlockValid(previousBlock))
                return APIResultCodes.InvalidPreviousBlock;

            // allow time drift: form -5 to +3
            var uniNow = DateTime.UtcNow;
            if (block is ServiceBlock bsb)
            {
                var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                if (board.LeaderCandidate != bsb.Leader)
                {
                    _log.LogWarning($"Invalid leader. was {bsb.Leader.Shorten()} should be {board.LeaderCandidate.Shorten()}");
                    return APIResultCodes.InvalidLeaderInServiceBlock;
                }

                var result = block.VerifySignature(board.LeaderCandidate);
                if (!result)
                {
                    _log.LogWarning($"VerifySignature failed for ServiceBlock Index: {block.Height} with Leader {board.CurrentLeader}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }

                if (sys.ConsensusState != BlockChainState.StaticSync)
                {
                    if (block.TimeStamp < uniNow.AddSeconds(-18) || block.TimeStamp > uniNow.AddSeconds(3))
                    {
                        _log.LogInformation($"TimeStamp 1: {block.TimeStamp} Universal Time Now: {uniNow}");
                        return APIResultCodes.InvalidBlockTimeStamp;
                    }
                }
            }
            else if (block is TransactionBlock)
            {
                var blockt = block as TransactionBlock;

                if (!blockt.VerifyHash())
                    _log.LogWarning($"VerifyBlock VerifyHash failed for TransactionBlock Index: {block.Height} by {block.GetHashInput()}");

                var verifyAgainst = blockt.AccountID;

                if (block.Height > 1 && previousBlock == null)
                    return APIResultCodes.InvalidPreviousBlock;

                if(block.ContainsTag(Block.MANAGEDTAG))
                {
                    if (block.Tags[Block.MANAGEDTAG] != "")
                        return APIResultCodes.InvalidManagementBlock;

                    //if (!(block is IBrokerAccount) && !(block is PoolFactoryBlock) && !(block is IPool))
                    //    return APIResultCodes.InvalidBrokerAcount;

                    var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                    verifyAgainst = board.CurrentLeader;
                }
                else
                {
                    if (block is IBrokerAccount)
                        return APIResultCodes.InvalidBrokerAcount;

                    if(block.Height > 1)
                    {
                        var firstBlock = await sys.Storage.FindFirstBlockAsync(blockt.AccountID);
                        if (firstBlock is IBrokerAccount || firstBlock.ContainsTag(Block.MANAGEDTAG))
                            return APIResultCodes.InvalidBrokerAcount;
                    }
                }

                if(previousBlock != null && previousBlock.ContainsTag(Block.MANAGEDTAG))
                {
                    if (!blockt.ContainsTag(Block.MANAGEDTAG))
                        return APIResultCodes.InvalidManagementBlock;

                    if (blockt.Tags[Block.MANAGEDTAG] != "")
                        return APIResultCodes.InvalidManagementBlock;

                    var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                    verifyAgainst = board.CurrentLeader;
                }

                var result = block.VerifySignature(verifyAgainst);
                if (!result)
                {
                    _log.LogWarning($"VerifyBlock failed for TransactionBlock Index: {block.Height} Type: {block.BlockType} by {blockt.AccountID}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }

                // check if this Index already exists (double-spending, kind of)
                if (await sys.Storage.FindBlockByIndexAsync(blockt.AccountID, block.Height) != null)
                    return APIResultCodes.BlockWithThisIndexAlreadyExists;

                // check service hash
                if (string.IsNullOrWhiteSpace(blockt.ServiceHash))
                    return APIResultCodes.ServiceBlockNotFound;

                var svcBlock = await sys.Storage.GetLastServiceBlockAsync();
                if (blockt.ServiceHash != svcBlock.Hash)
                {
                    // verify svc hash exists
                    var svc2 = await sys.Storage.FindBlockByHashAsync(blockt.ServiceHash);
                    if(svc2 == null)
                        return APIResultCodes.ServiceBlockNotFound;
                }                    

                //if (!await ValidateRenewalDateAsync(sys, blockt, previousBlock as TransactionBlock))
                //    return APIResultCodes.TokenExpired;

                if (sys.ConsensusState != BlockChainState.StaticSync)
                {
                    if (block.TimeStamp < uniNow.AddSeconds(-120) || block.TimeStamp > uniNow.AddSeconds(3))
                    {
                        _log.LogInformation($"TimeStamp 2: {block.TimeStamp} Universal Time Now: {uniNow}");
                        return APIResultCodes.InvalidBlockTimeStamp;
                    }
                }
            }
            else if (block is ConsolidationBlock cons)
            {
                if (sys.ConsensusState != BlockChainState.StaticSync)
                {
                    // time shift 10 seconds.
                    if (block.TimeStamp < uniNow.AddSeconds(-60) || block.TimeStamp > uniNow.AddSeconds(3))
                    {
                        _log.LogInformation($"TimeStamp 3: {block.TimeStamp} Universal Time Now: {uniNow}");
                        return APIResultCodes.InvalidBlockTimeStamp;
                    }
                }

                var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());
                if (board.CurrentLeader != cons.createdBy)
                {
                    _log.LogWarning($"Invalid leader. was {cons.createdBy.Shorten()} should be {board.CurrentLeader.Shorten()}");
                    return APIResultCodes.InvalidLeaderInConsolidationBlock;
                }

                var result = block.VerifySignature(board.CurrentLeader);
                if (!result)
                {
                    _log.LogWarning($"VerifySignature failed for ConsolidationBlock Index: {block.Height} with Leader {board.CurrentLeader}");
                    return APIResultCodes.BlockSignatureValidationFailed;
                }
            }
            else
            {
                return APIResultCodes.InvalidBlockType;
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
                throw new System.ApplicationException("APIResultCodes.InvalidBlockType");

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
