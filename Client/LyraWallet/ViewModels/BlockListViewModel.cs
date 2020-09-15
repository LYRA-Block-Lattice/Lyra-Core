using Lyra.Core.API;
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
                List<BlockInfo> blockInfos = await GetBlocks();

                Items.Clear();
                foreach (var item in blockInfos)
                {
                    Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                //await _thePage.DisplayAlert("Error", ex.Message, "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<List<BlockInfo>> GetBlocks()
        {
            var blocks = new List<BlockInfo>();
            var height = App.Store.State.wallet.GetLocalAccountHeight();
            for (var i = height; i > 0; i--)
            {
                var block = await App.Store.State.wallet.GetBlockByIndex(i);
                blocks.Add(new BlockInfo()
                {
                    index = block.Height,
                    timeStamp = block.TimeStamp,
                    hash = block.Hash,
                    type = block.BlockType.ToString(),
                    balance = block.Balances.Aggregate(new StringBuilder(),
                          (sb, kvp) => sb.AppendFormat("{0}{1} = {2}",
                                       sb.Length > 0 ? ", " : "", kvp.Key, kvp.Value.ToBalanceDecimal()),
                          sb => sb.ToString())
                });
            }
            return blocks;
        }

        public ICommand RefreshCommand { get; }

        public BlockListViewModel()
        {
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
