using LyraWallet.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace LyraWallet.Views
{
	[QueryProperty("Passenc", "passenc")]
	[XamlCompilation(XamlCompilationOptions.Compile)]
	public partial class NetworkSelectionPage : ContentPage
	{
		public NetworkSelectionPage ()
		{
			InitializeComponent ();

			Shell.SetTabBarIsVisible(this, false);
		}

		public string Passenc { set => (BindingContext as NetworkSelectionViewModel).Passenc = value; }

		protected async void OnClicked(object source, EventArgs args)
		{
			var bt = BindingContext as NetworkSelectionViewModel;

			var cap = new CreateAccountPage();
			cap.Passenc = bt.Passenc;
			cap.Network = bt.SelectedNetwork;

			await Navigation.PushAsync(cap);
		}
	}
}