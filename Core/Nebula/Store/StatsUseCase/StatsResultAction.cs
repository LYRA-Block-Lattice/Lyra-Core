using Core.Authorizers;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.StatsUseCase
{
	public class StatsResultAction
	{
		public List<TransStats> stats { get; }

		public StatsResultAction(List<TransStats> stats)
		{
			this.stats = stats;
		}
	}
}
