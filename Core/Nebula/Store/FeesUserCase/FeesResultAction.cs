using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.FeesUserCase
{
	public class FeesResultAction
	{
		public FeeStats stats { get; }
		public ServiceBlock view { get; }
		public List<Voter> voters { get; }

		public FeesResultAction(FeeStats stats, ServiceBlock view, List<Voter> voters)
		{
			this.stats = stats;
			this.view = view;
			this.voters = voters;
		}
	}
}
