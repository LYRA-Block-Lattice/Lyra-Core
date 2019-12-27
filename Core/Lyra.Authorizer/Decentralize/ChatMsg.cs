using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lyra.Authorizer.Decentralize
{
	public enum ChatMessageType { General, SeedChanged, NodeUp, NodeDown, AuthorizerPrePrepare, AuthorizerPrepare, AuthorizerCommit };

	public class SourceSignedMessage : SignableObject
	{
		/// <summary>
		/// Node Identify. Now it is AccountId
		/// </summary>
		public string From { get; set; }

		public override string GetHashInput()
		{
			throw new NotImplementedException();
		}

		protected override string GetExtraData()
		{
			throw new NotImplementedException();
		}
	}

	public class ChatMsg : SourceSignedMessage
	{
		public ChatMessageType Type { get; set; }
		public string Text { get; set; }

		public DateTime Created { get; set; } = DateTime.Now;
		public int Version { get; set; }
		public string NetworkId { get; set; }

		public ChatMsg()
		{

		}

		public ChatMsg(string author, string msg)
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
				this.Type.ToString() + "|" +
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
		public override string GetHashInput()
		{
			return $"{Block.UIndex}|{Block.GetHashInput()}";
		}

		protected override string GetExtraData()
		{
			return "";
		}
	}

	public class AuthorizedMsg : SourceSignedMessage
	{
		// block uindex, block hash (replace block itself), error code, authsign
		public long BlockIndex { get; set; }
		public APIResultCodes Result { get; set; }
		public AuthorizationSignature AuthSign { get; set; }
		public override string GetHashInput()
		{
			return $"{BlockIndex}|{Result}|{AuthSign?.Key}|{AuthSign?.Signature}|";
		}

		public bool IsSuccess => Result == APIResultCodes.Success;

		protected override string GetExtraData()
		{
			return "";
		}
	}

	public class AuthorizerCommitMsg : SourceSignedMessage
	{
		public long BlockIndex { get; set; }
		public bool Commited { get; set; }

		public bool IsSuccess => Commited;

		public override string GetHashInput()
		{
			return $"{BlockIndex}|{Commited}";
		}

		protected override string GetExtraData()
		{
			return "";
		}
	}
}
