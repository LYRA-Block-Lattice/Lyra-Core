using Core.Authorizers;
using Loyc.Collections.MutableListExtensionMethods;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nebula.Store.StatsUseCase
{
	public class StatsState
	{
		public bool IsLoading { get; }
		public List<TransStats> transStats { get; }

		public StatsState(bool isLoading, List<TransStats> transStats)
		{
			IsLoading = isLoading;
			this.transStats = transStats;
		}

		public TransStats Fastest => transStats.OrderBy(a => a.ms).FirstOrDefault();
		public TransStats Slowest => transStats.OrderBy(a => a.ms).LastOrDefault();
		public double AvgTime => Math.Round(transStats.Average(a => a.ms), 2);
		public double AvgSendTime => Math.Round(transStats.Where(a => a.trans == BlockTypes.SendTransfer).Average(b => b.ms), 2);
		public double AvgRecvTime => Math.Round(transStats.Where(a => a.trans == BlockTypes.ReceiveTransfer).Average(b => b.ms), 2);
	}
}
