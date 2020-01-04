using Lyra.Core.Blocks;
using Newtonsoft.Json;
using System;
using System.IO;

namespace Lyra.Core.Decentralize
{
	public enum ChatMessageType : byte { General, SeedChanged, NodeUp, NodeDown, AuthorizerPrePrepare, AuthorizerPrepare, AuthorizerCommit };

	public class SourceSignedMessage : SignableObject, Neo.IO.ISerializable
	{
		/// <summary>
		/// Node Identify. Now it is AccountId
		/// </summary>
		public string From { get; set; }
		public ChatMessageType MsgType { get; set; }

		public virtual int Size => From.Length + 1;

		public virtual void Deserialize(BinaryReader reader)
		{
			From = reader.ReadString();
			MsgType = (ChatMessageType)reader.ReadByte();
		}

		public virtual void Serialize(BinaryWriter writer)
		{
			writer.Write(From);
			writer.Write((byte)MsgType);
		}

		public override string GetHashInput()
		{
			return "";
		}

		protected override string GetExtraData()
		{
			return From;
		}
	}

	public class ChatMsg : SourceSignedMessage
	{
		public string Text { get; set; }

		public DateTime Created { get; set; } = DateTime.Now;
		public int Version { get; set; }
		public string NetworkId { get; set; }

		public ChatMsg()
		{
			MsgType = ChatMessageType.General;
		}

		public override int Size => base.Size +
			sizeof(ChatMessageType) +
			Text.Length +
			TimeSize +
			sizeof(int) +
			NetworkId.Length;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write((byte)MsgType);
			writer.Write(Text);
			writer.Write(Created.ToBinary());
			writer.Write(Version);
			writer.Write(NetworkId);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			MsgType = (ChatMessageType)reader.ReadByte();
			Text = reader.ReadString();
			Created = DateTime.FromBinary(reader.ReadInt64());
			Version = reader.ReadInt32();
			NetworkId = reader.ReadString();
		}

		private int TimeSize
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

		public ChatMsg(string author, string msg) : this()
		{
			From = author;
			Text = msg;
		}

		public override string GetHashInput()
		{
			return From + "|" +
				DateTimeToString(Created) + "|" +
				this.Version + "|" +
				this.NetworkId + "|" +
				this.From + "|" +
				this.MsgType.ToString() + "|" +
				this.Text + "|" +
				this.GetExtraData();
		}

		// should be overriden in specific instance to get the correct hash claculated from the entire block data 
		protected override string GetExtraData()
		{
			return string.Empty;
		}
	}

	public class AuthorizingMsg : SourceSignedMessage
	{
		public TransactionBlock Block { get; set; }

		public AuthorizingMsg()
		{
			MsgType = ChatMessageType.AuthorizerPrePrepare;
		}

		public override string GetHashInput()
		{
			return $"{Block.UIndex}|{Block.GetHashInput()}";
		}

		protected override string GetExtraData()
		{
			return "";
		}

		public override int Size => base.Size + JsonConvert.SerializeObject(Block).Length;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(JsonConvert.SerializeObject(Block));
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			Block = JsonConvert.DeserializeObject<TransactionBlock>(reader.ReadString());
		}
	}

	public class AuthorizedMsg : SourceSignedMessage
	{
		// block uindex, block hash (replace block itself), error code, authsign
		public long BlockIndex { get; set; }
		public APIResultCodes Result { get; set; }
		public AuthorizationSignature AuthSign { get; set; }

		public AuthorizedMsg()
		{
			MsgType = ChatMessageType.AuthorizerPrepare;
		}
		public override string GetHashInput()
		{
			return $"{BlockIndex}|{Result}|{AuthSign?.Key}|{AuthSign?.Signature}|";
		}

		public bool IsSuccess => Result == APIResultCodes.Success;

		protected override string GetExtraData()
		{
			return "";
		}

		public override int Size => base.Size + 
			sizeof(long) +
			sizeof(int) +
			JsonConvert.SerializeObject(AuthSign).Length;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(BlockIndex);
			writer.Write((int)Result);
			writer.Write(JsonConvert.SerializeObject(AuthSign));
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			BlockIndex = reader.ReadInt64();
			Result = (APIResultCodes)reader.ReadInt32();
			AuthSign = JsonConvert.DeserializeObject<AuthorizationSignature>(reader.ReadString());
		}
	}

	public class AuthorizerCommitMsg : SourceSignedMessage
	{
		public long BlockIndex { get; set; }
		public bool Commited { get; set; }

		public AuthorizerCommitMsg()
		{
			MsgType = ChatMessageType.AuthorizerCommit;
		}

		public bool IsSuccess => Commited;

		public override string GetHashInput()
		{
			return $"{BlockIndex}|{Commited}";
		}

		protected override string GetExtraData()
		{
			return "";
		}

		public override int Size => base.Size +
			sizeof(long) +
			sizeof(bool);

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(BlockIndex);
			writer.Write(Commited);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			BlockIndex = reader.ReadInt64();
			Commited = reader.ReadBoolean();
		}
	}
}
