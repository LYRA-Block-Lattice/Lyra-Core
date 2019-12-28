using Neo.IO;
using System.IO;

namespace Neo.Network.P2P.Payloads
{
    public interface IVerifiable : ISerializable
    {
        //Witness[] Witnesses { get; set; }

        void DeserializeUnsigned(BinaryReader reader);

        //UInt160[] GetScriptHashesForVerifying(Snapshot snapshot);

        void SerializeUnsigned(BinaryWriter writer);
    }
}
