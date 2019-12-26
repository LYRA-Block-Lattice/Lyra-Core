using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
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
		public SortedList<long, TransactionBlock> Blocks { get; set; }
		public override string GetHashInput()
		{
			var sb = new StringBuilder();
			sb.Append(From + "|");
			foreach(var b in Blocks)
			{
				sb.Append($"{b.Key}|{b.Value.GetHashInput()}|");
			}
			return sb.ToString();
		}

		protected override string GetExtraData()
		{
			return "";
		}
	}

	public class AuthorizedMsg : SourceSignedMessage
	{
		// block uindex, block hash (replace block itself), error code, authsign
		public SortedList<long, AuthSignForBlock> AuthResults { get; set; }
		public override string GetHashInput()
		{
			var sb = new StringBuilder();
			sb.Append(From + "|");
			foreach (var b in AuthResults)
			{
				sb.Append($"{b.Key}|{b.Value.Hash}|{b.Value.Result}|{b.Value.AuthSign?.Key}|{b.Value.AuthSign?.Signature}|");
			}
			return sb.ToString();
		}

		protected override string GetExtraData()
		{
			return "";
		}

		public class AuthSignForBlock
		{
			public string Hash { get; set; }
			public APIResultCodes Result { get; set; }
			public AuthorizationSignature AuthSign { get; set; }
		}
	}

	public class AuthorizerCommitMsg : SourceSignedMessage
	{
		public SortedList<long, bool> Commited { get; set; }

		public override string GetHashInput()
		{
			var sb = new StringBuilder();
			sb.Append(From + "|");
			foreach (var b in Commited)
			{
				sb.Append($"{b.Key}|{b.Value}|");
			}
			return sb.ToString();
		}

		protected override string GetExtraData()
		{
			return "";
		}
	}
}
