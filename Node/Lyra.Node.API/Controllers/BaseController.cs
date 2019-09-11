using System;

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Lyra.Core.Cryptography;

using Lyra.Core.Blocks;
using Lyra.Core.API;

namespace Lyra.Node.API.Controllers
{
    public abstract class BaseController : Controller
    {
        protected bool ValidateSignature(string AccountId, string Signature)
        {
            try
            {
                if (RPC.SyncHash == null)
                    RPC.SyncHash = GetSyncHash();

                var result = Signatures.VerifyAccountSignature(RPC.SyncHash, AccountId, Signature);

                // if validation failed, maybe our sync hash is not up to date; update and try again: 
                if (!result)
                    RPC.SyncHash = GetSyncHash();
                return Signatures.VerifyAccountSignature(RPC.SyncHash, AccountId, Signature);

            }
            catch 
            {
                // to do log
                return false;
            }
        }

        private string GetSyncHash()
        {
            var result = RPC.Client.GetSyncHeight().Result;
            if (!result.Successful())
                return null;
            return result.SyncHash;
        }

    }
}
