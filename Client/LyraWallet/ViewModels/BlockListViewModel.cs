using LyraWallet.Models;
using LyraWallet.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
    class BlockListViewModel : BaseViewModel
    {
        public ObservableCollection<BlockInfo> Items { get; set; }
        public Command LoadItemsCommand { get; set; }

        async Task ExecuteLoadItemsCommand()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                List<BlockInfo> blockInfos = await App.Container.GetBlocks();

                Items.Clear();
                foreach (var item in blockInfos)
                {
                    Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                await _thePage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public ICommand RefreshCommand { get; }

        private Page _thePage;
        public BlockListViewModel(Page page)
        {
            _thePage = page;
            Title = "Browse";
            Items = new ObservableCollection<BlockInfo>();
            LoadItemsCommand = new Command(async () => await ExecuteLoadItemsCommand());

            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, async (sender) =>
                {
                    await ExecuteLoadItemsCommand();
                });

            Task.Factory.StartNew(async () => await ExecuteLoadItemsCommand().ConfigureAwait(false));
        }
    }
}
