using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.NodeViewUseCase
{
	public class Feature : Feature<NodeViewState>
	{
		public override string GetName() => "NodeView";
		protected override NodeViewState GetInitialState() =>
			new NodeViewState(
				isLoading: false,
				billBoard: null,
				NodeStatus: null);
	}
}
