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
	[XamlCompilation(XamlCompilationOptions.Compile)]
    [QueryProperty("Network", "network")]
    public partial class CreateAccountPage : ContentPage
	{
        public string Network
        {
            set
            {
                (BindingContext as CreateAccountViewModel).NetworkId = Uri.UnescapeDataString(value);
            }
        }

        public CreateAccountPage ()
		{
            InitializeComponent ();

            var viewModel = new CreateAccountViewModel();
            BindingContext = viewModel;
        }
    }
}