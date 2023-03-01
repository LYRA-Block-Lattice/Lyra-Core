using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.ODR
{
    /// <summary>
    /// None: no proposal at all
    /// </summary>
    public enum ProposalType { None, DisputeResolution, DAOSettingChanges }
    /// <summary>
    /// Every vote must have a proposal which can be executed later to change something
    /// for OTC to clost a dispute
    /// for DAO to change arguments/settings
    /// </summary>
    public class VoteProposal
    {
        public ProposalType pptype { get; set; }
        public string data { get; set; }

        public object? Deserialize()
        {
            return pptype switch
            {
                ProposalType.None => null,
                ProposalType.DAOSettingChanges => JsonConvert.DeserializeObject<DAOChange>(data),
                ProposalType.DisputeResolution => JsonConvert.DeserializeObject<ODRResolution>(data),
                _ => null,
            };
        }
        public void Serialize(object obj)
        {
            if (obj is ODRResolution)
                pptype = ProposalType.DisputeResolution;
            else if(obj is DAOChange)
                pptype = ProposalType.DAOSettingChanges;

            data = JsonConvert.SerializeObject(obj);
        }

        public override string ToString()
        {
            return $"Type: {pptype} Content: {Deserialize().ToString()}";
        }
    }
}
