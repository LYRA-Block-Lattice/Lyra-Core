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
		public string requestSignature { get; set; }

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
		public string Candidate { get; set; }

		public ViewChangeReplyMessage()
		{
			MsgType = ChatMessageType.AuthorizerPrepare;
		}
		public override string GetHashInput()
		{
			return $"{Result}|{Candidate}" + base.GetHashInput();
		}

		public bool IsSuccess => Result == APIResultCodes.Success;

		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}

		public override int Size => base.Size +
			sizeof(int) +
			Candidate.Length;

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write((int)Result);
			writer.Write(Candidate);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			Result = (APIResultCodes)reader.ReadInt32();
			Candidate = reader.ReadString();
		}
	}

	public class ViewChangeCommitMessage : ViewChangeMessage
	{
		public string Candidate { get; set; }
		public ConsensusResult Consensus { get; set; }

		public ViewChangeCommitMessage()
		{
			MsgType = ChatMessageType.AuthorizerCommit;
		}

		public override string GetHashInput()
		{
			return $"{Candidate}|{Consensus}" + base.GetHashInput();
		}

		protected override string GetExtraData()
		{
			return base.GetExtraData();
		}

		public override int Size => base.Size +
			Candidate.Length +
			sizeof(ConsensusResult);

		public override void Serialize(BinaryWriter writer)
		{
			base.Serialize(writer);
			writer.Write(Candidate);
			writer.Write((int)Consensus);
		}

		public override void Deserialize(BinaryReader reader)
		{
			base.Deserialize(reader);
			Candidate = reader.ReadString();
			Consensus = (ConsensusResult)reader.ReadInt32();
		}
	}
}
