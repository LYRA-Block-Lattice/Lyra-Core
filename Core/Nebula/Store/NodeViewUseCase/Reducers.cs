using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.NodeViewUseCase
{
	public static class Reducers
	{
		[ReducerMethod]
		public static NodeViewState ReduceFetchDataAction(NodeViewState state, NodeViewAction action) =>
			new NodeViewState(
				isLoading: true,
				billBoard: null,
				NodeStatus: null,
				ipdb: null);

		[ReducerMethod]
		public static NodeViewState ReduceFetchDataResultAction(NodeViewState state, NodeViewResultAction action) =>
			new NodeViewState(
				isLoading: false,
				billBoard: action.billBoardResult,
				NodeStatus: action.nodeStatusResult,
				ipdb: action.ipDbFn);
	}
}
