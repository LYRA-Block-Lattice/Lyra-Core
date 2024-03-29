﻿using Lyra.Core.API;
using Lyra.Core.Blocks;
using NeoSmart.SecureStore;
//using Org.Apache.Http.Cookies;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lyra.Core.Accounts
{
    public class SecuredWalletStore : IAccountDatabase
    {
        private string _path;
        private Stream _stream;
        private string _name;
        private string _privateKey;
        private string _accountId;
        private string _networkId;
        private string _voteFor;
        private string _password;

        public SecuredWalletStore(string storagePath)
        {
            _path = storagePath;
            _stream = null;
        }

        public SecuredWalletStore(Stream stream)
        {
            _path = null;
            _stream = stream;
        }

        public string PrivateKey => _privateKey;

        public string NetworkId => _networkId;

        public string AccountId => _accountId;

        public string VoteFor { get => _voteFor; set { _voteFor = value; UpdateKvp("voteFor", _voteFor); } }

        public bool Create(string accountName, string password, string networkId, string privateKey, string accountId, string voteFor)
        {
            // A using block MUST be used for security reasons!
            using (var sman = SecretsManager.CreateStore())
            {
                // Create a new key securely with a CSPRNG:
                //sman.GenerateKey();
                // or use an existing key file:
                //sman.LoadKeyFromFile("path/to/file");
                // or securely derive key from passsword:
                sman.LoadKeyFromPassword(password);

                sman.Set("networkId", networkId);
                sman.Set("privateKey", privateKey);
                sman.Set("accountId", accountId);
                sman.Set("voteFor", voteFor);

                // Optionally export the keyfile (even if you created the store with a password)
                //sman.ExportKey("secrets.key");

                // Then save the store if you've made any changes to it
                if (_path == null)
                    sman.SaveStore(_stream);
                else
                    sman.SaveStore(name2fn(accountName));
            }
            return true;
        }

        private void UpdateKvp(string key, string value)
        {
            using (var sman = _path == null ? SecretsManager.LoadStore(_stream) : SecretsManager.LoadStore(name2fn(_name)))
            {
                sman.LoadKeyFromPassword(_password);

                sman.Set(key, value);

                // Optionally export the keyfile (even if you created the store with a password)
                //sman.ExportKey("secrets.key");

                // Then save the store if you've made any changes to it
                sman.SaveStore(name2fn(_name));
            }
        }

        private string name2fn(string name)
        {
            return $"{_path}{Path.DirectorySeparatorChar}{name}{LyraGlobal.WALLETFILEEXT}";
        }

        public void Delete(string accountName)
        {
            if (_path == null)
                return;

            var fn = name2fn(accountName);
            if (File.Exists(fn))
                File.Delete(fn);
        }

        public void Dispose()
        {
            if (_stream != null)
                _stream.Flush();
        }

        public bool Exists(string accountName)
        {
            if (_path == null)
                return false;
            else
                return File.Exists(name2fn(accountName));
        }

        public void Open(string accountName, string password)
        {
            _name = accountName;
            _password = password;
            using (var sman = _path == null ? SecretsManager.LoadStore(_stream) : SecretsManager.LoadStore(name2fn(_name)))
            {
                sman.LoadKeyFromPassword(password);

                _networkId = sman.Get("networkId");
                _privateKey = sman.Get("privateKey");
                _accountId = sman.Get("accountId");
                _voteFor = sman.Get("voteFor");
            }
        }
    }
}
