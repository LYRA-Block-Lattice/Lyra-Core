using Lyra.Core.API;
using Lyra.Core.Blocks;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Lyra.Core.Decentralize
{
	public enum ChatMessageType  
	{ 
		General = 0,
		HeartBeat = 1,
		BillBoardBroadcast = 3,

		Consensus = 5,

		NodeUp = 10,
		NodeDown = 11,
		NodeStatusInquiry = 12,
		NodeStatusReply = 13,
		
		BlockConsolidation = 20,

		AuthorizerPrePrepare = 100,
		AuthorizerPrepare = 101,
		AuthorizerCommit = 102,

		ViewChangeRequest = 105,
		ViewChangeReply = 106,
		ViewChangeCommit = 107
	};

	public class ProtocolException : Exception
    {
		public ProtocolException(string message) : base(message)
        {

        }
	}

	public class SourceSignedMessage : SignableObject, Neo.IO.ISerializable
	{
		/// <summary>
		/// Node Identify. Now it is AccountId
		/// </summary>
		public string From { get; set; }
		public ChatMessageType MsgType { get; set; }
		public int Version { get; set; } = LyraGlobal.ProtocolVersion;

		public virtual int Size => From.Length
			+ Hash.Length + Signature.Length
			+ sizeof(ChatMessageType)
			+ sizeof(int);

		public SourceSignedMessage()
        {

        }

		public virtual void Deserialize(BinaryReader reader)
		{
			Version = reader.ReadInt32();
			if (Version != LyraGlobal.ProtocolVersion)
				throw new ProtocolException($"Protocol mismatch. Local ver {LyraGlobal.ProtocolVersion}, remote ver {Version}");
			Hash = reader.ReadString();
			Signature = reader.ReadString();
			From = reader.ReadString();
			MsgType = (ChatMessageType)reader.ReadInt32();
		}

		public virtual void Serialize(BinaryWriter writer)
		{
			writer.Write(Version);
			writer.Write(Hash);
			writer.Write(Signature);
			writer.Write(From);
			writer.Write((int)MsgType);
		}

		public override string GetHashInput()
		{
			return $"{From}|{MsgType}|{Version}";
		}

		protected override string GetExtraData()
		{
			return "";
		}

		protected int TimeSize
		{
			get
			{
				int s;
				unsafe
				{
					s = sizeof(DateTime);
				}
				return s;
			}
		}
	}

	public class ChatMsg : SourceSignedMessage
	{
		private static readonly RNGCryptoServiceProvider random =
			new RNGCryptoServiceProvider();
		public string Text { get; set; }
		public long nonce { get; set; } 
		public DateTime timeStamp { get; set; }

		public ChatMsg()
		{
			MsgType = ChatMessageType.General;
			timeStamp = DateTime.UtcNow;

			var data = new byte[8];
			random.GetNonZeroBytes(data);
			nonce = BitConverter.ToInt64(data, 0);
		}
		public ChatMsg(string msg, ChatMessageType msgType)
		{
			MsgType = msgType;
			Text = msg;
			timeStamp = DateTime.UtcNow;

			var data = new byte[8];
			random.GetNonZeroBytes(data);
			nonce = BitConverter.ToInt64(data, 0);
		}

		public override int Size => base.Size + Text.Length + 16;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(Text);
			writer.Write(nonce);
			writer.Write(timeStamp.Ticks);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			Text = reader.ReadString();
			nonce = reader.ReadInt64();
			timeStamp = new DateTime(reader.ReadInt64(), DateTimeKind.Utc);
		}

		public override string GetHashInput()
		{
			return base.GetHashInput() + "|" +
				$"{nonce}|" + 
				$"{timeStamp.Ticks}|" +
				this.Text;
		}

		// should be overriden in specific instance to get the correct hash claculated from the entire block data 
		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}
	}

	public class HeartBeatMessage : ChatMsg
    {
		// sign against current service hash
		// so if current service block changed an outsynced node should never valid its heartbeat.
		public string AuthorizerSignature { get; set; }
		public BlockChainState State { get; set; }
		public string PublicIP { get; set; }
		public string NodeVersion { get; set; }
		public HeartBeatMessage()
        {
			MsgType = ChatMessageType.HeartBeat;
        }
		public override int Size => base.Size + AuthorizerSignature.Length + 1 + PublicIP.Length + NodeVersion.Length;
        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
			writer.Write(AuthorizerSignature);
			writer.Write((byte)State);
			writer.Write(PublicIP);
			writer.Write(NodeVersion);
        }
        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
			AuthorizerSignature = reader.ReadString();
			State = (BlockChainState)reader.ReadByte();
			PublicIP = reader.ReadString();
			NodeVersion = reader.ReadString();
		}

        public override string GetHashInput()
        {
			return base.GetHashInput() +
				$"|{NodeVersion}" +
				$"|{PublicIP}" +
				$"|{State}" +
				$"|{AuthorizerSignature}";
				
        }
    }

	public class ConsensusMessage : SourceSignedMessage
    {
	}

	public class BlockConsensusMessage: ConsensusMessage
	{
		public bool IsServiceBlock { get; set; }
		public string BlockHash { get; set; }

		public override int Size => base.Size + BlockHash.Length + 1;

		public BlockConsensusMessage()
        {
			IsServiceBlock = false;
        }

		public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            if (string.IsNullOrEmpty(BlockHash))
                throw new InvalidOperationException("BlockHash Should not be null");

			writer.Write(IsServiceBlock ? (byte)1 : (byte)0);
			writer.Write(BlockHash);
		}

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
			var b0 = reader.ReadByte();
			IsServiceBlock = b0 == 1;
			BlockHash = reader.ReadString();
		}

        public override string GetHashInput()
        {
            return IsServiceBlock.ToString() + "|" + BlockHash + "|" + base.GetHashInput();
        }
    }

	public class AuthorizingMsg : BlockConsensusMessage
	{
        public Block Block
        {
            get => _block; 
			set
            {
                _block = value;
                _blockJson = JsonConvert.SerializeObject(_block);
            }
        }
        private string _blockJson;
		private Block _block;

		public AuthorizingMsg()
		{
			MsgType = ChatMessageType.AuthorizerPrePrepare;
		}

		public override string GetHashInput()
		{
			return $"{Block.GetHashInput()}" + base.GetHashInput();
		}

		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}

		public override int Size => base.Size + _blockJson == null ? 1 : _blockJson.Length + 1;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write((byte)Block.BlockType);
			writer.Write(_blockJson);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			var typ = (BlockTypes)reader.ReadByte();
			var json = reader.ReadString();
			Block = GetBlock(typ, json);
		}

		protected Block GetBlock(BlockTypes blockType, string json)
		{
			var ar = new BlockAPIResult
			{
				ResultBlockType = blockType,
				BlockData = json
			};
			return ar.GetBlock();
		}
	}

	public class AuthorizedMsg : BlockConsensusMessage
	{
		// block uindex, block hash (replace block itself), error code, authsign
		public APIResultCodes Result { get; set; }
		public AuthorizationSignature AuthSign { get; set; }

		public AuthorizedMsg()
		{
			MsgType = ChatMessageType.AuthorizerPrepare;
		}
		public override string GetHashInput()
		{
			return $"{Result}|{AuthSign?.Key}|{AuthSign?.Signature}|" + base.GetHashInput();
		}

		public bool IsSuccess => Result == APIResultCodes.Success;

		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}

		public override int Size => base.Size +
			sizeof(int) +
			JsonConvert.SerializeObject(AuthSign).Length;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);			
			writer.Write((int)Result);
			writer.Write(JsonConvert.SerializeObject(AuthSign));
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			Result = (APIResultCodes)reader.ReadInt32();
			AuthSign = JsonConvert.DeserializeObject<AuthorizationSignature>(reader.ReadString());
		}
	}

	public class AuthorizerCommitMsg : BlockConsensusMessage
	{
		public ConsensusResult Consensus { get; set; }

		public AuthorizerCommitMsg()
		{
			MsgType = ChatMessageType.AuthorizerCommit;
		}

		public override string GetHashInput()
		{
			return $"{Consensus}" + base.GetHashInput();
		}

		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}

		public override int Size => base.Size +
			sizeof(ConsensusResult);

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write((int)Consensus);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			Consensus = (ConsensusResult)reader.ReadInt32();
		}
	}

	public class NodeStatus
	{
		public string accountId { get; set; }
		public string version { get; set; }
		public BlockChainState state { get; set; }
		public long totalBlockCount { get; set; }
		public string lastConsolidationHash { get; set; }
		public string lastUnSolidationHash { get; set; }
		public int activePeers { get; set; }
		public int connectedPeers { get; set; }

		public override bool Equals(object obj)
		{
			if(obj is NodeStatus)
			{
				var ns = obj as NodeStatus;
				return version == ns.version
					&& totalBlockCount == ns.totalBlockCount
					&& lastConsolidationHash == ns.lastConsolidationHash
					&& lastUnSolidationHash == ns.lastUnSolidationHash;				
			}
			return base.Equals(obj);
		}
	}
}
