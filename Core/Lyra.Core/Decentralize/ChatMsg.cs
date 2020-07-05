using Lyra.Core.API;
using Lyra.Core.Blocks;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Lyra.Core.Decentralize
{
	public enum ChatMessageType  
	{ 
		General = 0,
		HeartBeat = 1,
		BillBoardBroadcast = 3,

		NodeUp = 10,
		NodeDown = 11,
		NodeStatusInquiry = 12,
		NodeStatusReply = 13,
		
		BlockConsolidation = 20,

		AuthorizerPrePrepare = 100,
		AuthorizerPrepare = 101,
		AuthorizerCommit = 102,
	};

	public class SourceSignedMessage : SignableObject, Neo.IO.ISerializable
	{
		/// <summary>
		/// Node Identify. Now it is AccountId
		/// </summary>
		public string From { get; set; }
		public ChatMessageType MsgType { get; set; }
		public int Version { get; set; } = LyraGlobal.ProtocolVersion;
		//public DateTime Created { get; set; } = DateTime.Now;

		public virtual int Size => From.Length
			+ Hash.Length + Signature.Length
			+ sizeof(ChatMessageType)
			+ sizeof(int);
			//+ TimeSize;

		public virtual void Deserialize(BinaryReader reader)
		{
			Hash = reader.ReadString();
			Signature = reader.ReadString();
			From = reader.ReadString();
			MsgType = (ChatMessageType)reader.ReadInt32();
			Version = reader.ReadInt32();
			//Created = DateTime.FromBinary(reader.ReadInt64());
		}

		public virtual void Serialize(BinaryWriter writer)
		{
			writer.Write(Hash);
			writer.Write(Signature);
			writer.Write(From);
			writer.Write((int)MsgType);
			writer.Write(Version);
			//writer.Write(Created.ToBinary());
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
		public string Text { get; set; }

		public ChatMsg()
		{
			MsgType = ChatMessageType.General;
		}
		public ChatMsg(string msg, ChatMessageType msgType)
		{
			From = DagSystem.Singleton.PosWallet.AccountId;
			MsgType = msgType;
			Text = msg;
		}

		public override int Size => base.Size + Text.Length;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(Text);			
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			Text = reader.ReadString();
		}

		public override string GetHashInput()
		{
			return base.GetHashInput() + "|" +
				this.Text;
		}

		// should be overriden in specific instance to get the correct hash claculated from the entire block data 
		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}
	}

	public class AuthorizingMsg : SourceSignedMessage
	{
		public Block Block { get => _block; set 
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

	public class AuthorizedMsg : SourceSignedMessage
	{
		// block uindex, block hash (replace block itself), error code, authsign
		public string BlockHash { get; set; }
		public APIResultCodes Result { get; set; }
		public AuthorizationSignature AuthSign { get; set; }

		public AuthorizedMsg()
		{
			MsgType = ChatMessageType.AuthorizerPrepare;
		}
		public override string GetHashInput()
		{
			return $"{BlockHash}|{Result}|{AuthSign?.Key}|{AuthSign?.Signature}|" + base.GetHashInput();
		}

		public bool IsSuccess => Result == APIResultCodes.Success;

		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}

		public override int Size => base.Size +
			BlockHash.Length +
			sizeof(int) +
			JsonConvert.SerializeObject(AuthSign).Length;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(BlockHash);
			writer.Write((int)Result);
			writer.Write(JsonConvert.SerializeObject(AuthSign));
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			BlockHash = reader.ReadString();
			Result = (APIResultCodes)reader.ReadInt32();
			AuthSign = JsonConvert.DeserializeObject<AuthorizationSignature>(reader.ReadString());
		}
	}

	public class AuthorizerCommitMsg : SourceSignedMessage
	{
		public string BlockHash { get; set; }
		public ConsensusResult Consensus { get; set; }

		public AuthorizerCommitMsg()
		{
			MsgType = ChatMessageType.AuthorizerCommit;
		}

		public override string GetHashInput()
		{
			return $"{BlockHash}|{Consensus}" + base.GetHashInput();
		}

		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}

		public override int Size => base.Size +
			BlockHash.Length +
			sizeof(ConsensusResult);

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(BlockHash);
			writer.Write((int)Consensus);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			BlockHash = reader.ReadString();
			Consensus = (ConsensusResult)reader.ReadInt32();
		}
	}

	public class NodeStatus
	{
		public string accountId { get; set; }
		public string version { get; set; }
		public BlockChainState mode { get; set; }
		public long totalBlockCount { get; set; }
		public string lastConsolidationHash { get; set; }
		public string lastUnSolidationHash { get; set; }
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
