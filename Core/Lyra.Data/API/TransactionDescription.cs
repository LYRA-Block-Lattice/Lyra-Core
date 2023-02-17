using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Data.API
{
    // a simple description of the transaction.
    // this description need to be calculated
    public class TransactionDescription : IEquatable<TransactionDescription>
    {
        public long Height { get; set; }
        public bool IsReceive { get; set; }
        public DateTime TimeStamp { get; set; }
        public string SendAccountId { get; set; }
        public string SendHash { get; set; }
        public string RecvAccountId { get; set; }
        public string RecvHash { get; set; }
        public SortedDictionary<string, long> Changes { get; set; }
        public SortedDictionary<string, long> Balances { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as TransactionDescription);
        }

        public bool Equals(TransactionDescription other)
        {
            return other != null &&
                   Height == other.Height &&
                   IsReceive == other.IsReceive &&
                   TimeStamp == other.TimeStamp &&
                   SendAccountId == other.SendAccountId &&
                   SendHash == other.SendHash &&
                   RecvAccountId == other.RecvAccountId &&
                   RecvHash == other.RecvHash &&
                   EqualityComparer<SortedDictionary<string, long>>.Default.Equals(Changes, other.Changes) &&
                   EqualityComparer<SortedDictionary<string, long>>.Default.Equals(Balances, other.Balances);
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(Height);
            hash.Add(IsReceive);
            hash.Add(TimeStamp);
            hash.Add(SendAccountId);
            hash.Add(SendHash);
            hash.Add(RecvAccountId);
            hash.Add(RecvHash);
            hash.Add(Changes);
            hash.Add(Balances);
            return hash.ToHashCode();
        }

        public static bool operator ==(TransactionDescription left, TransactionDescription right)
        {
            return EqualityComparer<TransactionDescription>.Default.Equals(left, right);
        }

        public static bool operator !=(TransactionDescription left, TransactionDescription right)
        {
            return !(left == right);
        }
    }
}
