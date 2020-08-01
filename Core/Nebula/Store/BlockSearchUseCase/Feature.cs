using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.BlockSearchUseCase
{
	public class Feature : Feature<BlockSearchState>
	{
		public override string GetName() => "Block";
		protected override BlockSearchState GetInitialState() =>
			new BlockSearchState(
				isLoading: false,
				blockResult: null);
	}
}
