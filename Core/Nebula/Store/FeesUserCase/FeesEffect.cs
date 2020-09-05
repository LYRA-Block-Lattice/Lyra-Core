using Fluxor;
using Microsoft.AspNetCore.Components;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;
using Lyra.Core.API;
using Lyra.Core.Blocks;
using Akka;

namespace Nebula.Store.FeesUserCase
{
	public class FeesEffect : Effect<FeesAction>
	{
		private readonly LyraRestClient client;

		public FeesEffect(LyraRestClient lyraClient)
		{
			client = lyraClient;
		}

		protected override async Task HandleAsync(FeesAction action, IDispatcher dispatcher)
		{
			var stats = await client.GetFeeStatsAsync();
			var sbResult = await client.GetLastServiceBlock();
			var sb = sbResult.GetBlock() as ServiceBlock;
			var voters = await client.GetVotersAsync(new Lyra.Core.Decentralize.VoteQueryModel
			{
				posAccountIds = sb.Authorizers.Keys.ToList(),
				endTime = sb.TimeStamp
			});
			
			dispatcher.Dispatch(new FeesResultAction(stats, sb, voters));
		}
	}
}
