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
        public class MetaData
        {
            public string Name { get; set; }
            public string Note { get; set; }
            public byte[] Data { get; set; }
            public bool Backup { get; set; }
        }
        public event EventHandler OnChange;
        Dictionary<string, MetaData> _wallets;
        public WalletContainer(string json)
        {
            if(string.IsNullOrWhiteSpace(json))
                _wallets = new Dictionary<string, MetaData>();
            else
                _wallets = JsonConvert.DeserializeObject<Dictionary<string, MetaData>>(json);
        }

        public MetaData Get(string name) => _wallets[name];
        public string[] Names => _wallets.Keys.ToArray();

        public void AddOrUpdate(string name, byte[] data, string note, bool backup)
        {
            var meta = new MetaData { Name = name, Note = note, Data = data, Backup = backup };
            if (_wallets.ContainsKey(name))
                _wallets[name] = meta;
            else
                _wallets.Add(name, meta);

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
