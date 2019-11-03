using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.Models
{
    public class Product : IDBItem
    {
        public int ID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public Decimal Price { get; set; }
        public string PricingToken { get; set; }
        public string Unit { get; set; }
        public string ImageFn { get; set; }
        public string BarCode { get; set; }
    }
}
