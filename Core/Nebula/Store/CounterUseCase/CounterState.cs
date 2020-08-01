using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.CounterUseCase
{
	public class CounterState
	{
		public int ClickCount { get; }

		public CounterState(int clickCount)
		{
			ClickCount = clickCount;
		}
	}
}
