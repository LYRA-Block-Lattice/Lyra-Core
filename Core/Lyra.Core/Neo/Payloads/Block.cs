using Neo.IO;
using Neo.IO.Caching;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Network.P2P.Payloads
{
    public abstract class BlockBase : IVerifiable
    {
        public uint Version;
        public UInt256 PrevHash;
        public UInt256 MerkleRoot;
        public ulong Timestamp;
        public uint Index;
        public UInt160 NextConsensus;
        public Witness Witness;

        private UInt256 _hash = null;
        public UInt256 Hash
        {
            get
            {
                if (_hash == null)
                {
                    _hash = this.CalculateHash();
                }
                return _hash;
            }
        }

        public virtual int Size =>
            sizeof(uint) +       //Version
            UInt256.Length +     //PrevHash
            UInt256.Length +     //MerkleRoot
            sizeof(ulong) +      //Timestamp
            sizeof(uint) +       //Index
            UInt160.Length +     //NextConsensus
            1 +                  //Witness array count
            Witness.Size;        //Witness   

        Witness[] IVerifiable.Witnesses
        {
            get
            {
                return new[] { Witness };
            }
            set
            {
                if (value.Length != 1) throw new ArgumentException();
                Witness = value[0];
            }
        }

        public virtual void Deserialize(BinaryReader reader)
        {
            ((IVerifiable)this).DeserializeUnsigned(reader);
            Witness[] witnesses = reader.ReadSerializableArray<Witness>(1);
            if (witnesses.Length != 1) throw new FormatException();
            Witness = witnesses[0];
        }

        void IVerifiable.DeserializeUnsigned(BinaryReader reader)
        {
            Version = reader.ReadUInt32();
            PrevHash = reader.ReadSerializable<UInt256>();
            MerkleRoot = reader.ReadSerializable<UInt256>();
            Timestamp = reader.ReadUInt64();
            Index = reader.ReadUInt32();
            NextConsensus = reader.ReadSerializable<UInt160>();
        }

        UInt160[] IVerifiable.GetScriptHashesForVerifying(StoreView snapshot)
        {
            if (PrevHash == UInt256.Zero) return new[] { Witness.ScriptHash };
            Header prev_header = snapshot.GetHeader(PrevHash);
            if (prev_header == null) throw new InvalidOperationException();
            return new[] { prev_header.NextConsensus };
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            ((IVerifiable)this).SerializeUnsigned(writer);
            writer.Write(new Witness[] { Witness });
        }

        void IVerifiable.SerializeUnsigned(BinaryWriter writer)
        {
            writer.Write(Version);
            writer.Write(PrevHash);
            writer.Write(MerkleRoot);
            writer.Write(Timestamp);
            writer.Write(Index);
            writer.Write(NextConsensus);
        }

        public virtual JObject ToJson()
        {
            JObject json = new JObject();
            json["hash"] = Hash.ToString();
            json["size"] = Size;
            json["version"] = Version;
            json["previousblockhash"] = PrevHash.ToString();
            json["merkleroot"] = MerkleRoot.ToString();
            json["time"] = Timestamp;
            json["index"] = Index;
            json["nextconsensus"] = NextConsensus.ToAddress();
            json["witnesses"] = new JArray(Witness.ToJson());
            return json;
        }

        public virtual bool Verify(StoreView snapshot)
        {
            Header prev_header = snapshot.GetHeader(PrevHash);
            if (prev_header == null) return false;
            if (prev_header.Index + 1 != Index) return false;
            if (prev_header.Timestamp >= Timestamp) return false;
            if (!this.VerifyWitnesses(snapshot, 1_00000000)) return false;
            return true;
        }
    }
    public class Block : BlockBase
    {
        public const int MaxContentsPerBlock = ushort.MaxValue;
        public const int MaxTransactionsPerBlock = MaxContentsPerBlock - 1;

        public ConsensusData ConsensusData;
        public Transaction[] Transactions;
    }

    public class TrimmedBlock : BlockBase, ICloneable<TrimmedBlock>
    {
        public UInt256[] Hashes;
        public bool IsBlock => throw new NotImplementedException();// Hashes.Length > 0;

        private Header _header = null;
        public Header Header
        {
            get
            {
                if (_header == null)
                {
                    _header = new Header
                    {
                        Version = Version,
                        PrevHash = PrevHash,
                        MerkleRoot = MerkleRoot,
                        Timestamp = Timestamp,
                        Index = Index,
                        NextConsensus = NextConsensus,
                        Witness = Witness
                    };
                }
                return _header;
            }
        }
        public Block GetBlock(DataCache<UInt256, TransactionState> cache)
        {
            throw new NotImplementedException();
        }
        public TrimmedBlock Clone()
        {
            throw new NotImplementedException();
        }

        public void FromReplica(TrimmedBlock replica)
        {
            throw new NotImplementedException();
        }
    }

    public class Header : BlockBase, IEquatable<Header>
    {
        public override int Size => base.Size + 1;

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            if (reader.ReadByte() != 0) throw new FormatException();
        }

        public bool Equals(Header other)
        {
            if (other is null) return false;
            if (ReferenceEquals(other, this)) return true;
            return Hash.Equals(other.Hash);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Header);
        }

        public override int GetHashCode()
        {
            return Hash.GetHashCode();
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write((byte)0);
        }

        //public TrimmedBlock Trim()
        //{
        //    return new TrimmedBlock
        //    {
        //        Version = Version,
        //        PrevHash = PrevHash,
        //        MerkleRoot = MerkleRoot,
        //        Timestamp = Timestamp,
        //        Index = Index,
        //        NextConsensus = NextConsensus,
        //        Witness = Witness,
        //        Hashes = new UInt256[0]
        //    };
        //}
    }
}
