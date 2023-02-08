using Akka.Actor;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Lyra.Data.Crypto;
using Lyra.Core.Decentralize;
using Neo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lyra.Core.Decentralize.ConsensusService;
using Lyra.Data.API;
using Microsoft.Extensions.Logging;
using Lyra.Data.Shared;
using Lyra.Data.Utils;

namespace Lyra.Core.Authorizers
{
    public class ServiceAuthorizer : BaseAuthorizer
    {
        public override BlockTypes GetBlockType()
        {
            return BlockTypes.Service;
        }
        protected override async Task<APIResultCodes> AuthorizeImplAsync<T>(DagSystem sys, T tblock)
        {
            if (!(tblock is ServiceBlock))
                return APIResultCodes.InvalidBlockType;

            var block = tblock as ServiceBlock;

            var prevBlock = await sys.Storage.FindBlockByHashAsync(block.PreviousHash);

            // service specifice feature
            if (block.FeeTicker != LyraGlobal.OFFICIALTICKERCODE)
                return APIResultCodes.InvalidFeeTicker;

            var board = await sys.Consensus.Ask<BillBoard>(new AskForBillboard());

            if (prevBlock != null) // only if this is not very first service block
            {
                // verify fees
                var allConsBlocks = await sys.Storage.GetConsolidationBlocksAsync(prevBlock.Hash);
                var feesGened = allConsBlocks.Sum(a => a.totalFees);

                if (block.FeesGenerated != feesGened)
                    return APIResultCodes.InvalidServiceBlockTotalFees;

                var signAgainst = prevBlock?.Hash ?? ProtocolSettings.Default.StandbyValidators[0];

                foreach (var voter in board.AllVoters)
                {
                    var node = board.ActiveNodes.FirstOrDefault(a => a.AccountID == voter);
                    if (node != null && Signatures.VerifyAccountSignature(signAgainst, node.AccountID, node.AuthorizerSignature))
                    {
                        if (!block.Authorizers.ContainsKey(voter))
                        {
                            sys.Log($"svc block not include voter {voter}");
                            return APIResultCodes.InvalidAuthorizerInServiceBlock;
                        }                            
                    }
                }

                if(block.Authorizers.Keys.Any(a => !board.AllVoters.Contains(a)))
                {
                    sys.Log($"svc block has extra voter {block.Authorizers.Keys.FirstOrDefault(a => !board.AllVoters.Contains(a))}");
                    return APIResultCodes.InvalidAuthorizerInServiceBlock;
                }

                var myscvb = await sys.Consensus.Ask<ServiceBlock>(new AskForServiceBlock());
                if (!myscvb.AuthCompare(block))
                {
                    Console.WriteLine($"\nCompare service block, myscvb vs block");
                    Console.WriteLine(ObjectDumper.Dump(myscvb));
                    Console.WriteLine(ObjectDumper.Dump(block));
                    return APIResultCodes.BlockCompareFailed;
                }                    
            }
            else // svc gensis
            {
                if (block.Authorizers.Count < LyraGlobal.MINIMUM_AUTHORIZERS)
                    return APIResultCodes.InvalidAuthorizerCount;

                var signAgainst = ProtocolSettings.Default.StandbyValidators[0];
                foreach (var pn in ProtocolSettings.Default.StandbyValidators)
                {
                    if (!board.ActiveNodes.Any(a => a.AccountID == pn))
                        return APIResultCodes.InvalidAuthorizerInServiceBlock;

                    if (!Signatures.VerifyAccountSignature(signAgainst, pn, block.Authorizers[pn]))
                    {
                        return APIResultCodes.InvalidAuthorizerSignatureInServiceBlock;
                    }
                }
            }

            return await Lyra.Shared.StopWatcher.TrackAsync(() => base.AuthorizeImplAsync(sys, tblock), "ServiceAuthorizer->BaseAuthorizer");
        }

        protected override async Task<APIResultCodes> VerifyWithPrevAsync(DagSystem sys, Block block, Block previousBlock)
        {
            var bsb = block as ServiceBlock;
            var uniNow = DateTime.UtcNow;
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
                if (block.TimeStamp < uniNow.AddSeconds(-1 * LyraGlobal.VIEWCHANGE_TIMEOUT) || block.TimeStamp > uniNow.AddSeconds(3))
                {
                    _log.LogInformation($"TimeStamp 1: {block.TimeStamp} Universal Time Now: {uniNow}");
                    return APIResultCodes.InvalidBlockTimeStamp;
                }
            }

            return await base.VerifyWithPrevAsync(sys, block, previousBlock);
        }
    }
}
