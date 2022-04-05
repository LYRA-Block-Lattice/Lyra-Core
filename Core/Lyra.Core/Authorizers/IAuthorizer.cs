using Lyra.Core.Blocks;
using Lyra.Core.WorkFlow;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Authorizers
{
    public interface IAuthorizer
    {
        Task<AuthResult> AuthorizeAsync<T>(DagSystem sys, T tblock) where T : Block ;
        BlockTypes GetBlockType();
    }

    public class AuthResult : WrokflowAuthResult
    {
        public AuthorizationSignature Signature { get; set; }
    }
}
