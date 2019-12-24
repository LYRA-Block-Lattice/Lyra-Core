using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Authorizer.Decentralize
{
	public enum ChatMessageType { General, SeedChanged, NodeUp, NodeDown, AuthorizerPrePrepare, AuthorizerPrepare, AuthorizerCommit };

	public class ChatMsg : SignableObject
	{
		public DateTime Created { get; set; } = DateTime.Now;
		public int Version { get; set; }
		public string NetworkId { get; set; }
		/// <summary>
		/// Node Identify
		/// </summary>
		public string From { get; set; }
		public ChatMessageType Type { get; set; }
		public string Text { get; set; }

		// use BlockAPIResult to deserialize it
		public long BlockUIndex { get; set; }
		public TransactionBlock BlockToAuth { get; set; }

		// auth result. for prepare result can't be null
		public APIResultCodes AuthResult { get; set; }
		public AuthorizationSignature AuthSignature { get; set; }

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
				this.BlockUIndex.ToString() + "|" +
				JsonConvert.SerializeObject(BlockToAuth) + "|" +
				this.AuthResult.ToString() + "|" +
				JsonConvert.SerializeObject(AuthSignature) + "|" +
				this.GetExtraData();
		}

		// should be overriden in specific instance to get the correct hash claculated from the entire block data 
		protected override string GetExtraData()
		{
			return string.Empty;
		}
	}
}
