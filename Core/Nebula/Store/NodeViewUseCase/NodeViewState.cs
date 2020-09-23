using Converto;
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
		public ConcurrentDictionary<string, GetSyncStateAPIResult> nodeStatus { get; }

		public NodeViewState(bool isLoading, BillBoard billBoard, ConcurrentDictionary<string, GetSyncStateAPIResult> NodeStatus)
		{
			IsLoading = isLoading;
			bb = billBoard;
			nodeStatus = NodeStatus;
		}

		public List<NodeInfoSet> RankedList
        {
			get
            {
				var list = new List<NodeInfoSet>();
				foreach(var id in bb.PrimaryAuthorizers)
                {
					if(bb.ActiveNodes.Any(a => a.AccountID == id) && nodeStatus.ContainsKey(id))		// bug in billboard. or error-proof
                    {
						var x = bb.ActiveNodes.FirstOrDefault(a => a.AccountID == id);
						decimal vts = x == null ? 0 : x.Votes;
						list.Add(new NodeInfoSet
						{
							ID = id,
							IsPrimary = true,
							Votes = (long)vts,
							Status = nodeStatus[id]
						});
					}
                }

				var list2 = new List<NodeInfoSet>();
				var nonPrimaryNodes = nodeStatus.Where(a => !bb.PrimaryAuthorizers.Contains(a.Key));
				foreach(var node in nonPrimaryNodes)
                {
					var x = bb.ActiveNodes.FirstOrDefault(a => a.AccountID == node.Key);
					decimal vts = x == null ? 0 : x.Votes; 
					list2.Add(new NodeInfoSet
					{
						ID = node.Key,
						IsPrimary = false,
						Votes = (long)vts,
						Status = node.Value
					});
				}

				list.AddRange(list2);
				return list.OrderByDescending(a => a.Votes)
					.ThenBy(b => b.ID)
					.Zip(Enumerable.Range(1, int.MaxValue - 1),
									  (o, i) => o.With(new { Index = i }))
					.ToList();
			}
        }
	}

	public class NodeInfoSet
    {
		public int Index { get; set; }
		public string ID { get; set; }
		public bool IsPrimary { get; set; }
		public long Votes { get; set; }
		public GetSyncStateAPIResult Status { get; set; }
	}
}
