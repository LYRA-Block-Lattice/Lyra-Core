using Fluxor;
using Microsoft.AspNetCore.Components;
using Nebula.Store.StatsUseCase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Pages
{
    public partial class Stats
    {
		[Inject]
		private IState<StatsState> statsState { get; set; }

		[Inject]
		private IDispatcher Dispatcher { get; set; }

		protected override void OnInitialized()
		{
			base.OnInitialized();
			Dispatcher.Dispatch(new StatsAction());
		}
	}
}
