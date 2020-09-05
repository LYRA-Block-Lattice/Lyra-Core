using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.StatsUseCase
{
	public static class Reducers
	{
		[ReducerMethod]
		public static StatsState ReduceFeeAction(StatsState state, StatsAction action) =>
			new StatsState(
				isLoading: true,
				transStats: null);

		[ReducerMethod]
		public static StatsState ReduceFeeResultAction(StatsState state, StatsResultAction action) =>
			new StatsState(
				isLoading: false,
				transStats: action.stats);
	}
}
