using Loyc.Collections.MutableListExtensionMethods;
using Lyra.Core.Accounts;
using Lyra.Core.Blocks;
using Lyra.Data.API;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nebula.Store.FeesUserCase
{
	public class FeesState
	{
		public bool IsLoading { get; }
		public FeeStats Stats { get; }
		public ServiceBlock View { get; }
		public List<Voter> Voters { get; }

		public FeesState(bool isLoading, FeeStats stats, ServiceBlock view, List<Voter> voters)
		{
			IsLoading = isLoading;
			Stats = stats;
			View = view;
			Voters = voters;
		}

		public List<RevItem> ConfirmedRevs()
        {
			return null;
        }

		public List<RevItem> UnConfirmedRevs()
        {
			return null;
        }

		public class RevItem
		{
			public string accId { get; set; }
			public decimal revenue { get; set; }
		}
	}
}
