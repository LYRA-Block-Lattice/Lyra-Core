using Fluxor;
using Microsoft.AspNetCore.Components;
using Nebula.Store.BlockSearchUseCase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Pages
{
	public partial class ShowBlock
	{
		[Parameter] 
		public string hash { get; set; }

		[Inject]
		private IState<BlockSearchState> searchState { get; set; }

		[Inject]
		private IDispatcher Dispatcher { get; set; }

		protected override void OnInitialized()
		{
			base.OnInitialized();
			Dispatcher.Dispatch(new BlockSearchAction(hash));
		}

        protected override async Task OnParametersSetAsync()
        {
			Dispatcher.Dispatch(new BlockSearchAction(hash));

			await base.OnParametersSetAsync();
        }

        public void oninput(ChangeEventArgs args)
		{
			Dispatcher.Dispatch(new BlockSearchAction(args.Value.ToString()));
		}
	}
}
