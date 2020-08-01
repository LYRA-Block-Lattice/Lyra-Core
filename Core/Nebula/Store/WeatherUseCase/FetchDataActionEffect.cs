using Fluxor;
using Microsoft.AspNetCore.Components;
using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Json;

namespace Nebula.Store.WeatherUseCase
{
	public class FetchDataActionEffect : Effect<FetchDataAction>
	{
		private readonly HttpClient Http;

		public FetchDataActionEffect(HttpClient http)
		{
			Http = http;
		}

		protected override async Task HandleAsync(FetchDataAction action, IDispatcher dispatcher)
		{
			//var forecasts = await Http.GetFromJsonAsync<WeatherForecast[]>("WeatherForecast");
			var forecasts = await (new WeatherForecastService()).GetForecastAsync(DateTime.Now);
			dispatcher.Dispatch(new FetchDataResultAction(forecasts));
		}
	}
}
