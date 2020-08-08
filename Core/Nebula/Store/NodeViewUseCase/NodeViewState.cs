﻿using Lyra.Core.API;
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
					if(bb.AllNodes.ContainsKey(id))		// bug in billboard. or error-proof
                    {
						list.Add(new NodeInfoSet
						{
							ID = id,
							IsPrimary = true,
							Votes = bb.AllNodes[id].Votes,
							Status = nodeStatus[id]
						});
					}
                }

				var list2 = new List<NodeInfoSet>();
				var nonPrimaryNodes = nodeStatus.Where(a => !bb.PrimaryAuthorizers.Contains(a.Key));
				foreach(var node in nonPrimaryNodes)
                {
					list2.Add(new NodeInfoSet
					{
						ID = node.Key,
						IsPrimary = false,
						Votes = bb.AllNodes[node.Key].Votes,
						Status = node.Value
					});
				}

				list.AddRange(list2.OrderByDescending(a => a.Votes));
				return list;
			}
        }
	}

	public class NodeInfoSet
    {
		public string ID;
		public bool IsPrimary;
		public decimal Votes;
		public GetSyncStateAPIResult Status;
	}
}
