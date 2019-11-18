﻿using System;
using System.Collections.Generic;
using System.Text;

namespace LyraWallet.Models
{
    public class BlockInfo
    {
        public int index { get; set; }
        public DateTime timeStamp { get; set; }
        public string hash { get; set; }
        public string type { get; set; }
        public string balance { get; set; }
    }
}
