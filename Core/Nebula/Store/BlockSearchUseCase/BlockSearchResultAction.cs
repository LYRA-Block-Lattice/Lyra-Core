using Lyra.Core.Blocks;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.BlockSearchUseCase
{
	public class BlockSearchResultAction
	{
		public Block block { get; }
		public string key { get; }
		public long maxHeight { get; }

		public BlockSearchResultAction(Block resultBlock, string resultKey, long resultHeight)
		{
			block = resultBlock;
			key = resultKey;
			maxHeight = resultHeight;
		}
	}
}
