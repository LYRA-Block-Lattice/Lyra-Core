using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.Models
{
    public class BlockInfo
    {
        public long index { get; set; }
        public DateTime timeStamp { get; set; }
        public string hash { get; set; }
        public string type { get; set; }
        public string balance { get; set; }

        public string action { get; set; }
        public string account { get; set; }
        public string diffrence { get; set; }
    }
}
