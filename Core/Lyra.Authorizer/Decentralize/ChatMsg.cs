using System;
using System.Collections.Generic;
using System.Text;

namespace Lyra.Authorizer.Decentralize
{
	public enum ChatMessageType { NewLeader, NewStaker };

	public class ChatMsg
	{
		public DateTimeOffset Created { get; set; } = DateTimeOffset.Now;
		/// <summary>
		/// Node Identify
		/// </summary>
		public string From { get; set; }
		public ChatMessageType Type { get; set; }
		public string Text { get; set; }


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
