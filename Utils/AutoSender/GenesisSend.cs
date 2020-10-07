using Lyra.Core.Accounts;
using Lyra.Core.Decentralize;
using Lyra.Data.API;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AutoSender
{
    public class GenesisSend
    {
        private string _netName;
        private string _walletName;
        private string _fnBillBoard;

        public GenesisSend(string netName, string walletName, string fnBillBoard)
        {
            _netName = netName;
            _walletName = walletName;
            _fnBillBoard = fnBillBoard;
        }

        public async Task Send()
        {
            // open wallet
            Wallet wallet = null;// await ShadowWallet.OpenAsync(_netName, _walletName);

            var bb = JsonConvert.DeserializeObject<BillBoard>(File.ReadAllText(_fnBillBoard));

            var amount = 2000000;

            foreach (var pa in bb.PrimaryAuthorizers)
            {
                await wallet.Sync(null);
                var result = await wallet.Send(amount, pa);
                if(result.ResultCode == Lyra.Core.Blocks.APIResultCodes.Success)
                {
                    Console.WriteLine($"Success sent {amount} to {pa}");
                }
            }

            //amount = 1500000;
            //foreach (var ba in bb.BackupAuthorizers)
            //{
            //    await wallet.Sync(null);
            //    var result = await wallet.Send(amount, ba);
            //    if (result.ResultCode == Lyra.Core.Blocks.APIResultCodes.Success)
            //    {
            //        Console.WriteLine($"Success sent {amount} to {ba}");
            //    }
            //}
        }
    }
}
