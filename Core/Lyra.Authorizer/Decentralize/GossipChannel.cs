using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Orleans;
using Orleans.Streams;

namespace Lyra.Authorizer.Decentralize
{
	public class GossipChannel : Grain, IGossipChannel
	{
		private readonly List<ChatMsg> messages = new List<ChatMsg>(100);
		private readonly List<string> onlineMembers = new List<string>(10);

		private IAsyncStream<ChatMsg> stream;

		public override Task OnActivateAsync()
		{
			var streamProvider = GetStreamProvider(Constants.LyraGossipStreamProvider);

		    stream = streamProvider.GetStream<ChatMsg>(Guid.NewGuid(), Constants.LyraGossipStreamNameSpace);
            return base.OnActivateAsync();
		}

		public async Task<Guid> Join(string nickname)
		{
			onlineMembers.Add(nickname);

			await stream.OnNextAsync(new ChatMsg("System", $"{nickname} joins the chat '{this.GetPrimaryKeyString()}' ..."));

			return stream.Guid;
		}

		public async Task<Guid> Leave(string nickname)
		{
			onlineMembers.Remove(nickname);
			await stream.OnNextAsync(new ChatMsg("System", $"{nickname} leaves the chat..."));

			return stream.Guid;
		}

		public async Task<bool> Message(ChatMsg msg)
		{
			messages.Add(msg);
			await stream.OnNextAsync(msg);

			return true;
		}

	    public Task<string[]> GetMembers()
	    {
	        return Task.FromResult(onlineMembers.ToArray());
	    }

	    public Task<ChatMsg[]> ReadHistory(int numberOfMessages)
	    {
	        var response = messages
	            .OrderByDescending(x => x.Created)
	            .Take(numberOfMessages)
	            .OrderBy(x => x.Created)
	            .ToArray();

	        return Task.FromResult(response);
	    }
    }

	public static class Constants
	{
		public const string LyraGossipStreamProvider = "pbft";
		public const string LyraGossipStreamNameSpace = "lyra";
	}

	public static class PrettyConsole
	{
		public static void Line(string text, ConsoleColor colour = ConsoleColor.White)
		{
			var originalColour = Console.ForegroundColor;
			Console.ForegroundColor = colour;
			Console.WriteLine(text);
			Console.ForegroundColor = originalColour;
		}
	}
}