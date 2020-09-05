using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.FeesUserCase
{
	public class Feature : Feature<FeesState>
	{
		public override string GetName() => "Fees";
		protected override FeesState GetInitialState() =>
			new FeesState(
				isLoading: false,
				stats: null,
				view: null,
				voters: null);
	}
}
