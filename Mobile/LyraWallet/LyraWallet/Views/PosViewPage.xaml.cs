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
    public partial class PosViewPage : ContentPage
    {
        PosViewModel viewModel;
        public PosViewPage()
        {
            InitializeComponent();
            BindingContext = viewModel = new PosViewModel();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            if (viewModel.Products.Count == 0)
                viewModel.LoadItemsCommand.Execute(null);
        }

        public void DeleteClicked(object sender, EventArgs e)
        {
            var item = (Xamarin.Forms.Button)sender;
            viewModel.RemoveProductCommand.Execute((int)item.CommandParameter);
        }
    }
}