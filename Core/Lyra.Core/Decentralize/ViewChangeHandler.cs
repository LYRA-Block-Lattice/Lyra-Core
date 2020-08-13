using Lyra.Core.Utils;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Core.Decentralize
{
    public class ViewChangeHandler : ConsensusHandlerBase
    {

        public ViewChangeHandler(ConsensusService context) : base(context)
        {

        }
        public void HandleRequest()
        {

        }

        internal Task ProcessMessage(ViewChangeMessage vcm)
        {
            throw new NotImplementedException();
        }
    }


}
