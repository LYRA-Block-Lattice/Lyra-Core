using Nebula.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WeatherUseCase
{
	public class WeatherState
	{
		public bool IsLoading { get; }
		public IEnumerable<WeatherForecast> Forecasts { get; }

		public WeatherState(bool isLoading, IEnumerable<WeatherForecast> forecasts)
		{
			IsLoading = isLoading;
			Forecasts = forecasts ?? Array.Empty<WeatherForecast>();
		}
	}
}
