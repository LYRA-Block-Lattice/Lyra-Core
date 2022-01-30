using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lyra.Data.API.Identity
{
    public class TxMessage : TxRecord
    {
        public override MessageTypes MessageType => MessageTypes.Text;

        public string Text { get; set; }

        public override string Print()
        {
            return base.Print() +
                "Text: " + Text;
        }

        protected override string GetExtraData()
        {
            return base.GetExtraData() +
                this.Text + "|";
        }
    }
}
