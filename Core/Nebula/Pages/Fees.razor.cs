using Fluxor;
using Microsoft.AspNetCore.Components;
using Nebula.Store.FeesUserCase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Pages
{
    public partial class Fees
    {
		[Parameter]
		public string addr { get; set; }
		[Parameter]
		public long height { get; set; }

		[Inject]
		private IState<FeesState> feeState { get; set; }

		[Inject]
		private IDispatcher Dispatcher { get; set; }

		protected override void OnInitialized()
		{
			base.OnInitialized();
			Dispatcher.Dispatch(new FeesAction());
		}
	}
}
