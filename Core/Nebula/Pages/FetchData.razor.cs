using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Nebula.Store.WeatherUseCase;

namespace Nebula.Pages
{
	public partial class FetchData
	{
		[Inject]
		private IState<WeatherState> WeatherState { get; set; }

		[Inject]
		private IDispatcher Dispatcher { get; set; }

		protected override void OnInitialized()
		{
			base.OnInitialized();
			Dispatcher.Dispatch(new FetchDataAction());
		}
	}
}
