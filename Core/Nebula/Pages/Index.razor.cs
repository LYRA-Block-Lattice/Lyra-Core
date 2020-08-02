using Fluxor;
using Microsoft.AspNetCore.Components;
using Nebula.Store.BlockSearchUseCase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nebula.Pages
{
	public partial class Index
	{
		[Inject]
		public NavigationManager navigationManager { get; set; }

		public void oninput(ChangeEventArgs args)
		{
			navigationManager.NavigateTo($"/showblock/{args.Value}");
		}
	}
}
