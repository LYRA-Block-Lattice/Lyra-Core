using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.BlockSearchUseCase
{
	public class BlockSearchState
	{
		public bool IsLoading { get; }
		public Block block { get; }

		public BlockSearchState(bool isLoading, Block blockResult)
		{
			IsLoading = isLoading;
			block = blockResult ?? null;
		}
	}
}
