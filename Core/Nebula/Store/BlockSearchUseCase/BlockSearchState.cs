using Loyc.Collections.MutableListExtensionMethods;
using Lyra.Core.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

		public string FancyShow()
        {
			var r = new Regex(@"BlockType: \w+");
			var html = r.Replace(block.Print(), Matcher);

			html = Regex.Replace(html, @"\s(\w{44})\W", HashMatcher);

			return html;
        }

		private string HashMatcher(Match m)
        {
			var all = m.Groups[0].Value;
			var hash = m.Groups[1].Value;

			if (hash == block.Hash)
				return all;
			else
				return all.Replace(hash, $"<a href='/showblock/{hash}'>{hash}</a>");
		}

		private string Matcher(Match m)
        {
			return $"<b style='color: blue'>{m.Groups.FirstOrDefault()}</b>";
		}
	}
}
