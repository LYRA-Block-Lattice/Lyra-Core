using Lyra.Core.Blocks;
using Lyra.Core.Blocks.Transactions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Authorizer.Decentralize
{
	public enum ChatMessageType { General, OperatorEvent, NodeEvent, AuthorizerPrePrepare, AuthorizerPrepare, AuthorizerCommit };

	public class ChatMsg
	{
		public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
		/// <summary>
		/// Node Identify
		/// </summary>
		public string From { get; set; }
		public ChatMessageType Type { get; set; }
		public string Text { get; set; }

		// use BlockAPIResult to deserialize it
		public long BlockUIndex { get; set; }
		public APIResultCodes AuthResult { get; set; }
		public TransactionBlock BlockToAuth { get; set; }

		public ChatMsg()
		{

		}

		public ChatMsg(string author, string msg)
		{
			From = author;
			Text = msg;
		}
	}
}
