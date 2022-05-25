using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API
{
    public class WalletContainer
    {
        public class WalletData
        {
            public string Name { get; set; } = null!;
            public string? Note { get; set; }
            public byte[] Data { get; set; } = null!;
            public bool Backup { get; set; }
            public string? DealerID { get; set; }
        }

        public event EventHandler? OnChange;
        Dictionary<string, WalletData>? _wallets;
        public WalletContainer(string json)
        {
            if(string.IsNullOrWhiteSpace(json))
                _wallets = new Dictionary<string, WalletData>();
            else
                _wallets = JsonConvert.DeserializeObject<Dictionary<string, WalletData>>(json);

            if (_wallets == null)
                throw new Exception("Invalid Wallet Data");
        }

        public WalletData Get(string name) => _wallets[name];
        public string[] Names => _wallets.Keys.ToArray();

        public void Add(string name, byte[] data, string note, bool backup, string dealerId)
        {
            Add(new WalletData
            {
                Name = name, Data = data, Note = note, Backup = backup, DealerID = dealerId
            });
        }

        public void Add(WalletData wallet)
        {
            if (_wallets.ContainsKey(wallet.Name))
                throw new Exception($"Wallet exists.");
            else
                _wallets.Add(wallet.Name, wallet);

            OnChange?.Invoke(this, new EventArgs());
        }

        public void Update(WalletData wallet)
        {
            if (!_wallets.ContainsKey(wallet.Name))
                throw new Exception($"Wallet not exists.");
            else
                _wallets[wallet.Name] = wallet;

            OnChange?.Invoke(this, new EventArgs());
        }

        public void Remove(string name)
        {
            _wallets.Remove(name);
            OnChange?.Invoke(this, new EventArgs());
        }        

        public override string ToString()
        {
            return JsonConvert.SerializeObject(_wallets, Formatting.Indented);
        }
    }
}
