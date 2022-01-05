using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize.WorkFlow
{
    public class WFDao
    {
        internal static async Task<APIResultCodes> CreateDaoPreAuthAsync(DagSystem sys, SendTransferBlock send, TransactionBlock lastblock)
        {
            if (
                send.Tags.ContainsKey("name") && !string.IsNullOrWhiteSpace(send.Tags["name"])
                && send.Tags.Count == 2
                )
            {
                if (send.DestinationAccountId != PoolFactoryBlock.FactoryAccount)
                    return APIResultCodes.InvalidServiceRequest;

                // check name dup
                var name = send.Tags["name"];
                var existsdao = sys.Storage.GetDaoByName(name);
                if (existsdao != null)
                    return APIResultCodes.DuplicateName;

                return APIResultCodes.Success;
            }
            else
                return APIResultCodes.InvalidBlockTags;
        }

        internal static Task<TransactionBlock> CNOCreateDaoAsync(DagSystem sys, SendTransferBlock send)
        {
            throw new NotImplementedException();
        }
    }
}
