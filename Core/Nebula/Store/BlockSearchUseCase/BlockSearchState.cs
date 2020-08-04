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

		public string Key { get; }
		public long MaxHeight { get; }

		public long prevHeight => block.Height > 1 ? block.Height - 1 : block.Height;
		public long nextHeight => block.Height < MaxHeight ? block.Height + 1 : block.Height;
		public bool IsBlockValid => block.Hash.Equals(block.CalculateHash());

		public BlockSearchState(bool isLoading, Block blockResult, string pageKey, long maxHeight)
		{
			IsLoading = isLoading;
			block = blockResult ?? null;
			Key = pageKey;
			MaxHeight = maxHeight;
		}

		public string FancyShow()
        {
			var r = new Regex(@"BlockType: \w+");
			var html = r.Replace(block.Print(), Matcher);

			html = Regex.Replace(html, @"\s(\w{43,})\W", HashMatcher);

			return html;
        }

		private string HashMatcher(Match m)
        {
			var all = m.Groups[0].Value;
			var hash = m.Groups[1].Value;

			if (hash == block.Hash)
				return all;
			if (hash.Length == 43 || hash.Length == 44 || (hash.Length > 90 && hash.StartsWith('L')))
				return all.Replace(hash, $"<a href='/showblock/{hash}'>{hash}</a>");
			else
				return all;
		}

		private string Matcher(Match m)
        {
			return $"<b style='color: blue'>{m.Groups.FirstOrDefault()}</b>";
		}
	}
}
