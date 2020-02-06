using LiteDB;
using LyraWallet.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace LyraWallet.Services
{
    public class DBStoreService<T> : IDataStore<T> where T : IDBItem
    {
        static LiteDatabase _db;

        public DBStoreService()
        {
            if(_db == null)
            {
                var dbFn = DependencyService.Get<IPlatformSvc>().GetStoragePath() + "pos.db";
                string connectionString = "Filename=" + dbFn;
                _db = new LiteDatabase(connectionString);
            }
        }

        public Task<bool> AddItemAsync(T item)
        {
            var coll = _db.GetCollection<T>();
            var maxID = coll.Max(a => a.ID);
            item.ID = maxID + 1;
            coll.Insert(item);
            return Task.FromResult(true);
        }

        public Task<int> DeleteItemAsync(int id)
        {
            var ret = _db.GetCollection<T>().Delete(a => a.ID == id);
            return Task.FromResult(ret);
        }

        public Task<T> GetItemAsync(int id)
        {
            var item = _db.GetCollection<T>().Find(x => x.ID == id).First();
            return Task.FromResult(item);
        }

        public Task<IEnumerable<T>> GetItemsAsync(bool forceRefresh = false)
        {
            var items = _db.GetCollection<T>().FindAll();
            return Task.FromResult(items);
        }

        public Task<bool> UpdateItemAsync(T item)
        {
            var ret = _db.GetCollection<T>().Update(item);
            return Task.FromResult(ret);
        }
    }
}
