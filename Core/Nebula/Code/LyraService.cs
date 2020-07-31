using Lyra.Core.Decentralize;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Nebula.Code
{
    public class LyraService
    {
        private readonly HttpClient httpClient;

        public LyraService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public string GetBaseUrl()
        {
            return httpClient.BaseAddress.ToString();
        }

        public async Task<BillBoard> GetBillBoard()
        {
            var addr = "api/Node/GetBillBoard";
            return await httpClient.GetFromJsonAsync<BillBoard>(addr);
        }
    }
}
