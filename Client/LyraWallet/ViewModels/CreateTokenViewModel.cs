using LyraWallet.Services;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;

namespace LyraWallet.ViewModels
{
    public class CreateTokenViewModel : BaseViewModel
    {
        private string _tokenName;
        public string TokenName
        {
            get => _tokenName;
            set => SetProperty(ref _tokenName, value);
        }

        private string _domainName;
        public string DomainName
        {
            get => _domainName;
            set => SetProperty(ref _domainName, value);
        }
        private string _description;
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
        private string _totalSupply;
        public string TotalSupply
        {
            get => _totalSupply;
            set => SetProperty(ref _totalSupply, value);
        }
        private string _precision;
        public string Precision
        {
            get => _precision;
            set => SetProperty(ref _precision, value);
        }
        private string _ownerName;
        public string OwnerName
        {
            get => _ownerName;
            set => SetProperty(ref _ownerName, value);
        }
        private string _ownerAddress;
        public string OwnerAddress
        {
            get => _ownerAddress;
            set => SetProperty(ref _ownerAddress, value);
        }

        public ICommand CreateTokenCommand { get; }

        private Page _thePage;
        public CreateTokenViewModel(Page page)
        {
            _thePage = page;
            CreateTokenCommand = new Command(async () =>
            {
                try
                {
                    var total = decimal.Parse(TotalSupply);

                    await App.Container.CreateToken(TokenName, DomainName,
                            Description, Decimal.Parse(TotalSupply), int.Parse(Precision), OwnerName, OwnerAddress);

                    await _thePage.DisplayAlert("Success", "Your token is created and ready to use. Goto balance and refresh to see.", "OK");
                }
                catch (Exception x)
                {
                    await _thePage.DisplayAlert("Error", x.Message, "OK");
                }
            });
        }
    }
}
