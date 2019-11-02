using Lyra.Core.Blocks.Transactions;
using LyraWallet.Models;
using LyraWallet.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
    public class PosViewModel : BaseViewModel
    {
        DBStoreService<Product> _products;
        public ObservableCollection<Product> Products { get; set; }
        public string ProductTitle { get; set; }
        public string ProductPrice { get; set; }

        public int SelectedIndex { get; set; }
        public string SelectedTokenName { get; set; }
        private List<string> _tokenName;
        public List<string> TokenNames
        {
            get => _tokenName;
            set => SetProperty(ref _tokenName, value);
        }

        public Command LoadItemsCommand { get; set; }
        public Command AddProductCommand { get; set; }
        public Command RemoveProductCommand { get; set; }

        public PosViewModel()
        {
            _products = new DBStoreService<Product>();

            Products = new ObservableCollection<Product>();
            LoadItemsCommand = new Command(async () => await ExecuteLoadItemsCommand());
            AddProductCommand = new Command(async () => {
                var product = new Product()
                {
                    Title = ProductTitle,
                    Price = decimal.Parse(ProductPrice),
                    PriceByToken = SelectedTokenName
                };
                await _products.AddItemAsync(product);
                await ExecuteLoadItemsCommand();
            });
            RemoveProductCommand = new Command<int>(async (id) => {
                await _products.DeleteItemAsync(id);
                await ExecuteLoadItemsCommand();
            });
            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, (sender) =>
                {
                    TokenNames = App.Container.TokenList;
                    if (TokenNames != null)
                    {
                        for(int i = 0; i < TokenNames.Count; i++)
                        {
                            if (TokenNames[i] == TokenGenesisBlock.LYRA_TICKER_CODE)
                                SelectedIndex = i;
                        }
                    }
                });
        }

        async Task ExecuteLoadItemsCommand()
        {
            if (IsBusy)
                return;

            IsBusy = true;

            try
            {
                Products.Clear();
                var items = await _products.GetItemsAsync();
                foreach (var item in items)
                {
                    Products.Add(item);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

}
