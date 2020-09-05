using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.FeesUserCase
{
	public static class Reducers
	{
		[ReducerMethod]
		public static FeesState ReduceFeeAction(FeesState state, FeesAction action) =>
			new FeesState(
				isLoading: true,
				stats: null,
				view: null,
				voters: null);

		[ReducerMethod]
		public static FeesState ReduceFeeResultAction(FeesState state, FeesResultAction action) =>
			new FeesState(
				isLoading: false,
				stats: action.stats,
				view: action.view,
				voters: action.voters);
	}
}
