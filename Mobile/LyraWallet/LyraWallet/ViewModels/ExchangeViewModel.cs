using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace LyraWallet.ViewModels
{
    public class ExchangeViewModel : BaseViewModel
    {
        private List<string> _tokenList;
        public List<string> TokenList
        {
            get
            {
                return _tokenList;
            }
            set
            {
                SetProperty(ref _tokenList, value);
            }
        }

        public string SelectedToken { get; set; }
        public string FilterKeyword { get; set; }

        public ExchangeViewModel()
        {

        }

        internal async Task Touch()
        {
            // load token list
            var _client = new HttpClient
            {
                //BaseAddress = new Uri("https://localhost:5001/api/")
                BaseAddress = new Uri("http://lex.lyratokens.com:5493/api/"),
#if DEBUG
                Timeout = new TimeSpan(0, 30, 0)        // for debug. but 10 sec is too short for real env
#else
                Timeout = new TimeSpan(0, 0, 30)
#endif
            };

            try
            {
                string responseBody = await _client.GetStringAsync("Exchange");
                TokenList = JsonConvert.DeserializeObject<List<string>>(responseBody);
            }
            catch(Exception)
            {
                
            }
        }
    }
}
