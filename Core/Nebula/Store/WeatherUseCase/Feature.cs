using Fluxor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Store.WeatherUseCase
{
	public class Feature : Feature<WeatherState>
	{
		public override string GetName() => "Weather";
		protected override WeatherState GetInitialState() =>
			new WeatherState(
				isLoading: false,
				forecasts: null);
	}
}
