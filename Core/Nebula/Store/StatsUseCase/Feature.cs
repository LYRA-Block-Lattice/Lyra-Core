using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.StatsUseCase
{
	public class Feature : Feature<StatsState>
	{
		public override string GetName() => "Stats";
		protected override StatsState GetInitialState() =>
			new StatsState(
				isLoading: false,
				transStats: null);
	}
}
