using Lyra.Core.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public class ConsensusRuntimeConfig
    {
        public string Mode { get; set; }
        public List<string> Seeds { get; set; }
        public string CurrentSeed { get; set; }
        public List<AuthorizerNode> PrimaryAuthorizerNodes { get; set; }
        public List<AuthorizerNode> BackupAuthorizerNodes { get; set; }
        public List<AuthorizerNode> VotingNodes { get; set; }

        public List<AuthorizerNode> GetAllNodes()
        {
            return PrimaryAuthorizerNodes.Concat(BackupAuthorizerNodes).Concat(VotingNodes).ToList();
        }
    }

    public class AuthorizerNode
    {
        public string Address { get; set; }
        public string AccountID { get; set; }
        public decimal StakingAmount { get; set; }

        public override string ToString()
        {
            return $"{Address} Staking: {StakingAmount} {LyraGlobal.OFFICIALTICKERCODE} with AccountID: {AccountID}";
        }
    }
}
