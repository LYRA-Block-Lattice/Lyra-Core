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

        private ILiteCollection<T> Products => _db.GetCollection<T>("Products", BsonAutoId.Int32);

        public Task<bool> AddItemAsync(T item)
        {
            Products.Insert(item);
            _db.Commit();
            return Task.FromResult(true);
        }

        public Task<int> DeleteItemAsync(int id)
        {
            var ret = Products.Delete(id);
            _db.Commit();
            return Task.FromResult(id);
        }

        public Task<T> GetItemAsync(int id)
        {
            var item = Products.Find(x => x.ID == id).First();
            return Task.FromResult(item);
        }

        public Task<IEnumerable<T>> GetItemsAsync(bool forceRefresh = false)
        {
            var items = Products.FindAll();
            return Task.FromResult(items);
        }

        public Task<bool> UpdateItemAsync(T item)
        {
            var ret = Products.Update(item);
            _db.Commit();
            return Task.FromResult(ret);
        }
    }
}
