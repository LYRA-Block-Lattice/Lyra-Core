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
			long maxHeight = 0;
			string key = null;
			if(string.IsNullOrWhiteSpace(hashToSearch))
            {
				var genSvcRet = await client.GetLastConsolidationBlock();
				if(genSvcRet.ResultCode == APIResultCodes.Success)
                {
					blockResult = genSvcRet.GetBlock();					
				}
            }
			else
            {
				BlockAPIResult ret = null;
				if(hashToSearch.Length < 40)
                {
					ret = await client.GetServiceBlockByIndex(action.hash, action.height);
                }
				else if(hashToSearch.Length == 44 || hashToSearch.Length == 43)	// hash
                {
					ret = await client.GetBlock(action.hash);
				}
				else
                {
					var exists = await client.GetAccountHeight(action.hash);
					if(exists.ResultCode == APIResultCodes.Success)
                    {
						maxHeight = exists.Height;
						key = action.hash;
						ret = await client.GetBlockByIndex(action.hash, action.height == 0 ? exists.Height : action.height);
                    }
                }
				
				if (ret != null && ret.ResultCode == APIResultCodes.Success)
				{
					blockResult = ret.GetBlock();
				}
			}

			(key, maxHeight) = await GetMaxHeightAsync(blockResult);
			
			dispatcher.Dispatch(new BlockSearchResultAction(blockResult, key, maxHeight));
		}

		private async Task<(string, long)> GetMaxHeightAsync(Block block)
        {
			BlockAPIResult lastBlockResult = null;
			switch(block)
            {
				case ServiceBlock sb:
					lastBlockResult = await client.GetLastServiceBlock();
					break;
				case ConsolidationBlock cb:
					lastBlockResult = await client.GetLastConsolidationBlock();
					break;
				case TransactionBlock tb:
					var tbLastResult = await client.GetAccountHeight(tb.AccountID);
					if (tbLastResult.ResultCode == APIResultCodes.Success)
						return (tb.AccountID, tbLastResult.Height);
					break;
				default:
					break;
            }

			if (lastBlockResult != null && lastBlockResult.ResultCode == APIResultCodes.Success)
            {
				var lb = lastBlockResult.GetBlock();
				return (lb.BlockType.ToString(), lb.Height);
			}				
			else
				return (null, 0);
        }
	}
}
