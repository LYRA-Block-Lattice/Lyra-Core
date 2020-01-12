using Akka.IO;
using Lyra.Core.Decentralize;
using Neo.Cryptography;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using System;
using System.IO;

namespace Neo.Network.P2P
{
    public class Message : ISerializable
    {
        public const int PayloadMaxSize = 0x02000000;
        private const int CompressionMinSize = 128;
        private const int CompressionThreshold = 64;

        public MessageFlags Flags;
        public MessageCommand Command;
        public ISerializable Payload;

        private byte[] _payload_compressed;

        public int Size => sizeof(MessageFlags) + sizeof(MessageCommand) + _payload_compressed.GetVarSize();

        public static Message Create(MessageCommand command, ISerializable payload = null)
        {
            Message message = new Message
            {
                Flags = MessageFlags.None,
                Command = command,
                Payload = payload,
                _payload_compressed = payload?.ToArray() ?? new byte[0]
            };

            // Try compression
            if (message._payload_compressed.Length > CompressionMinSize)
            {
                var compressed = message._payload_compressed.CompressLz4();
                if (compressed.Length < message._payload_compressed.Length - CompressionThreshold)
                {
                    message._payload_compressed = compressed;
                    message.Flags |= MessageFlags.Compressed;
                }
            }

            return message;
        }

        private void DecompressPayload()
        {
            if (_payload_compressed.Length == 0) return;
            byte[] decompressed = Flags.HasFlag(MessageFlags.Compressed)
                ? _payload_compressed.DecompressLz4(PayloadMaxSize)
                : _payload_compressed;
            switch (Command)
            {
                case MessageCommand.Version:
                    Payload = decompressed.AsSerializable<VersionPayload>();
                    break;
                case MessageCommand.Addr:
                    Payload = decompressed.AsSerializable<AddrPayload>();
                    break;
                case MessageCommand.Ping:
                case MessageCommand.Pong:
                    Payload = decompressed.AsSerializable<PingPayload>();
                    break;
                case MessageCommand.GetHeaders:
                //case MessageCommand.GetBlocks:
                //    Payload = decompressed.AsSerializable<GetBlocksPayload>();
                //    break;
                //case MessageCommand.Headers:
                //    Payload = decompressed.AsSerializable<HeadersPayload>();
                    break;
                case MessageCommand.Inv:
                case MessageCommand.GetData:
                    Payload = decompressed.AsSerializable<InvPayload>();
                    break;
                //case MessageCommand.Transaction:
                //    Payload = decompressed.AsSerializable<Transaction>();
                //    break;
                //case MessageCommand.Block:
                //    Payload = decompressed.AsSerializable<Block>();
                //    break;
                case MessageCommand.Consensus:
                    Payload = DecodeSignedMessage(decompressed);
                    break;
                    //case MessageCommand.FilterLoad:
                    //    Payload = decompressed.AsSerializable<FilterLoadPayload>();
                    //    break;
                    //case MessageCommand.FilterAdd:
                    //    Payload = decompressed.AsSerializable<FilterAddPayload>();
                    //    break;
                    //case MessageCommand.MerkleBlock:
                    //    Payload = decompressed.AsSerializable<MerkleBlockPayload>();
                    //    break;
            }
        }

        private SourceSignedMessage DecodeSignedMessage(byte[] data)
        {
            var sm = data.AsSerializable<SourceSignedMessage>();
            switch(sm.MsgType)
            {
                case ChatMessageType.AuthorizerPrePrepare:
                    return data.AsSerializable<AuthorizingMsg>();
                case ChatMessageType.AuthorizerPrepare:
                    return data.AsSerializable<AuthorizedMsg>();
                case ChatMessageType.AuthorizerCommit:
                    return data.AsSerializable<AuthorizerCommitMsg>();
                case ChatMessageType.General:
                case ChatMessageType.NodeUp:
                case ChatMessageType.NodeDown:
                case ChatMessageType.HeartBeat:
                case ChatMessageType.StakingChanges:
                    return data.AsSerializable<ChatMsg>();
                default:
                    return null;
            }
        }

        void ISerializable.Deserialize(BinaryReader reader)
        {
            Flags = (MessageFlags)reader.ReadByte();
            Command = (MessageCommand)reader.ReadByte();
            _payload_compressed = reader.ReadVarBytes(PayloadMaxSize);
            DecompressPayload();
        }

        void ISerializable.Serialize(BinaryWriter writer)
        {
            writer.Write((byte)Flags);
            writer.Write((byte)Command);
            writer.WriteVarBytes(_payload_compressed);
        }

        internal static int TryDeserialize(ByteString data, out Message msg)
        {
            msg = null;
            if (data.Count < 3) return 0;

            var header = data.Slice(0, 3).ToArray();
            var flags = (MessageFlags)header[0];
            ulong length = header[2];
            var payloadIndex = 3;

            if (length == 0xFD)
            {
                if (data.Count < 5) return 0;
                length = data.Slice(payloadIndex, 2).ToArray().ToUInt16(0);
                payloadIndex += 2;
            }
            else if (length == 0xFE)
            {
                if (data.Count < 7) return 0;
                length = data.Slice(payloadIndex, 4).ToArray().ToUInt32(0);
                payloadIndex += 4;
            }
            else if (length == 0xFF)
            {
                if (data.Count < 11) return 0;
                length = data.Slice(payloadIndex, 8).ToArray().ToUInt64(0);
                payloadIndex += 8;
            }

            if (length > PayloadMaxSize) throw new FormatException();

            if (data.Count < (int)length + payloadIndex) return 0;

            msg = new Message()
            {
                Flags = flags,
                Command = (MessageCommand)header[1],
                _payload_compressed = length <= 0 ? new byte[0] : data.Slice(payloadIndex, (int)length).ToArray()
            };
            msg.DecompressPayload();

            return payloadIndex + (int)length;
        }
    }
}
