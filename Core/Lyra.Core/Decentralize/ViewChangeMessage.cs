using Lyra.Core.Blocks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Lyra.Core.Decentralize
{
    public class ViewChangeMessage : ConsensusMessage
	{
		public long ViewID { get; set; }

		public ViewChangeMessage()
		{
			MsgType = ChatMessageType.Consensus;
		}

		public override int Size => base.Size + sizeof(long);

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(ViewID);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			ViewID = reader.ReadInt64();
		}

		public override string GetHashInput()
		{
			return ViewID + "|" + base.GetHashInput();
		}
	}

	public class ViewChangeRequestMessage : ViewChangeMessage
	{
		public long prevViewID { get; set; }
		public string prevLeader { get; set; }

		public ViewChangeRequestMessage()
		{
			MsgType = ChatMessageType.ViewChangeRequest;
		}

		public override string GetHashInput()
		{
			return $"{prevViewID}|{prevLeader}|" + base.GetHashInput();
		}

		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}

		public override int Size => base.Size + sizeof(long) + prevLeader.Length;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(prevViewID);
			writer.Write(prevLeader);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			prevViewID = reader.ReadInt64();
			prevLeader = reader.ReadString();
		}
	}

	public class ViewChangeReplyMessage : ViewChangeMessage
	{
		// block uindex, block hash (replace block itself), error code, authsign
		public APIResultCodes Result { get; set; }
		public AuthorizationSignature AuthSign { get; set; }

		public ViewChangeReplyMessage()
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

	public class ViewChangeCommitMessage : ViewChangeMessage
	{
		public ConsensusResult Consensus { get; set; }

		public ViewChangeCommitMessage()
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
}
