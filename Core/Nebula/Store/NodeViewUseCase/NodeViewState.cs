using Lyra.Core.API;
using Lyra.Core.Decentralize;
using Nebula.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.NodeViewUseCase
{
	public class NodeViewState
	{
		public bool IsLoading { get; }
		public BillBoard bb { get; }
		public ConcurrentDictionary<string, GetSyncStateAPIResult> nodeStatus {get;}

		public NodeViewState(bool isLoading, BillBoard billBoard, ConcurrentDictionary<string, GetSyncStateAPIResult> NodeStatus)
		{
			IsLoading = isLoading;
			bb = billBoard;
			nodeStatus = NodeStatus;
		}
	}
}
