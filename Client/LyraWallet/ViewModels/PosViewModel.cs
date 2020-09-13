using Lyra.Core.API;
using LyraWallet.Models;
using LyraWallet.Services;
using LyraWallet.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
    public class PosViewModel : BaseViewModel
    {
        DBStoreService<Product> _products;
        public ObservableCollection<CartItem> CartItems { get; set; }
        public string ProductTitle { get; set; }
        public string ProductPrice { get; set; }

        public int SelectedIndex { get; set; }
        private string selectedTokenName;
        private List<string> _tokenName;
        public List<string> TokenNames
        {
            get => _tokenName;
            set => SetProperty(ref _tokenName, value);
        }
        private bool addCartMode;
        private bool productEditMode;
        private int editorSize;
        private int cartCount;
        private Decimal cartTotal;
        private string pricingToken;

        public Command LoadItemsCommand { get; set; }
        public Command AddProductCommand { get; set; }
        public Command RemoveProductCommand { get; set; }
        public Command AddCartCommand { get; set; }
        public Command RemoveCartCommand { get; set; }
        public Command CheckoutCommand { get; set; }
        public string SelectedTokenName { get => selectedTokenName; set => SetProperty(ref selectedTokenName, value); }
        public bool ProductEditMode { get => productEditMode; set => SetProperty(ref productEditMode, value); }
        public int EditorSize { get => editorSize; set => SetProperty(ref editorSize, value); }
        public bool AddCartMode { get => addCartMode; set => SetProperty(ref addCartMode, value); }
        public int CartCount { get => cartCount; set => SetProperty(ref cartCount, value); }
        public decimal CartTotal { get => cartTotal; set => SetProperty(ref cartTotal, value); }
        public string PricingToken { get => pricingToken; set => SetProperty(ref pricingToken, value); }
        public FontAttributes FontAttributes { get; private set; } = FontAttributes.Bold;
        public Color BadgeColor { get; private set; } = Color.LightGreen;
        public Color BadgeTextColor { get; private set; } = Color.Black; 
        
        public Page ThePage { get; set; }        

        public PosViewModel()
        {
            AddCartMode = true;
            ProductEditMode = false;
            _products = new DBStoreService<Product>();
            CartCount = 0;
            CartTotal = 0;

            CartItems = new ObservableCollection<CartItem>();
            LoadItemsCommand = new Command(async () => await ExecuteLoadItemsCommand());
            AddProductCommand = new Command(async () => {
                var product = new Product()
                {
                    Title = ProductTitle,
                    Price = decimal.Parse(ProductPrice),
                    PricingToken = SelectedTokenName
                };
                await _products.AddItemAsync(product);
                await ExecuteLoadItemsCommand();
            });
            RemoveProductCommand = new Command<int>(async (id) => {
                await _products.DeleteItemAsync(id);
                await ExecuteLoadItemsCommand();
            });
            AddCartCommand = new Command<int>(async (id) => {
                var item = CartItems.First(a => a.product.ID == id);
                if(PricingToken != null && PricingToken != item.product.PricingToken)
                {
                    // only one token type can be added to cart 
                    await ThePage.DisplayAlert("Error", $"Only products pricing by {PricingToken} can be added.", "OK");
                    return;
                }

                if(PricingToken == null)
                    PricingToken = item.product.PricingToken;

                item.Count++;
                CartCount++;
                CartTotal += item.product.Price;
            });
            RemoveCartCommand = new Command<int>(async (id) => {
                var item = CartItems.First(a => a.product.ID == id);
                if (item.Count > 0)
                {
                    CartTotal -= item.product.Price;
                    CartCount--;
                    item.Count--;

                    if(CartCount == 0)
                    {
                        PricingToken = null;
                    }
                }                    
            });
            CheckoutCommand = new Command(async () =>
            {
                if(CartTotal > 0)
                {
                    var nextPage = new PosCheckoutPage(CartItems.Where(a => a.Count > 0).ToList());
                    await ThePage.Navigation.PushAsync(nextPage);
                }
            });

            MessagingCenter.Subscribe<BalanceViewModel>(
                this, MessengerKeys.BalanceRefreshed, (sender) =>
                {
                    TokenNames = App.Store.State.wallet.GetLatestBlock().Balances?.Keys.ToList();
                    if (TokenNames != null)
                    {
                        for(int i = 0; i < TokenNames.Count; i++)
                        {
                            if (TokenNames[i] == LyraGlobal.OFFICIALTICKERCODE)
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
                CartItems.Clear();
                var items = await _products.GetItemsAsync();
                foreach (var item in items)
                {
                    CartItems.Add(new CartItem { product = item, Count = 0 });
                }
                CartCount = 0;
                CartTotal = 0;
                PricingToken = null;
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
