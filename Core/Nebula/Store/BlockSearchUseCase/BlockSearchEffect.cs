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

namespace Nebula.Store.BlockSearchUseCase
{
	public class BlockSearchEffect : Effect<BlockSearchAction>
	{
		private readonly LyraRestClient client;

		public BlockSearchEffect(LyraRestClient lyraClient)
		{
			client = lyraClient;
		}

		protected override async Task HandleAsync(BlockSearchAction action, IDispatcher dispatcher)
		{
			var hashToSearch = action.hash;
			Block blockResult = null;
			if(string.IsNullOrWhiteSpace(hashToSearch))
            {
				var genSvcRet = await client.GetServiceGenesisBlock();
				if(genSvcRet.ResultCode == APIResultCodes.Success)
                {
					blockResult = genSvcRet.GetBlock();					
				}
            }
			else
            {
				var ret = await client.GetBlock(action.hash);
				if (ret.ResultCode == APIResultCodes.Success)
				{
					blockResult = ret.GetBlock();
				}
			}

			dispatcher.Dispatch(new BlockSearchResultAction(blockResult));
		}
	}
}
