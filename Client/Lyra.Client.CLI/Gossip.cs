using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Lyra.Authorizer.Decentralize;

namespace Lyra.Client.CLI
{
    public class Gossip
    {
        //To make this sample simple
        //In this sample, one client can only join one channel, hence we have a static variable of one channel name.
        //client can send messages to the channel , and receive messages sent to the channel/stream from other clients. 
        private static Guid joinedChannel = Guid.NewGuid();//"general";
        private static string userName = "UserWithNoName";
        public static void Main(IClusterClient client)
        {
            var clientInstance = client;

            PrettyConsole.Line("==== CLIENT: Initialized ====", ConsoleColor.Cyan);
            PrettyConsole.Line("CLIENT: Write commands:", ConsoleColor.Cyan);


            Menu(clientInstance).GetAwaiter().GetResult();

            PrettyConsole.Line("==== CLIENT: Shutting down ====", ConsoleColor.DarkRed);
        }

        private static void PrintHints()
        {
            var menuColor = ConsoleColor.Magenta;
            PrettyConsole.Line("Type '/j <channel>' to join specific channel", menuColor);
            PrettyConsole.Line("Type '/n <username>' to set your user name", menuColor);
            PrettyConsole.Line("Type '/l' to leave specific channel", menuColor);
            PrettyConsole.Line("Type '<any text>' to send a message", menuColor);
            PrettyConsole.Line("Type '/h' to re-read channel history", menuColor);
            PrettyConsole.Line("Type '/m' to query members in the channel", menuColor);
            PrettyConsole.Line("Type '/exit' to exit client.", menuColor);
        }

        private static async Task Menu(IClusterClient client)
        {
            string input;
            PrintHints();

            do
            {
                input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input)) continue;

                if (input.StartsWith("/j"))
                {
                    await JoinChannel(client, input.Replace("/j", "").Trim());
                }
                else if (input.StartsWith("/n"))
                {
                    userName = input.Replace("/n", "").Trim();
                    PrettyConsole.Line($"Your user name is set to be {userName}", ConsoleColor.DarkGreen);
                }
                else if (input.StartsWith("/l"))
                {
                    await LeaveChannel(client);
                }
                else if (input.StartsWith("/h"))
                {
                    await ShowCurrentChannelHistory(client);
                }
                else if (input.StartsWith("/m"))
                {
                    await ShowChannelMembers(client);
                }
                else if (!input.StartsWith("/exit"))
                {
                    await SendMessage(client, input);
                }
            } while (input != "/exit");
        }

        private static async Task ShowChannelMembers(IClusterClient client)
        {
            var room = client.GetGrain<ILyraGossip>(joinedChannel);
            var members = await room.GetMembers();

            PrettyConsole.Line($"====== Members for '{joinedChannel}' Channel ======", ConsoleColor.DarkGreen);
            foreach (var member in members)
            {
                PrettyConsole.Line(member, ConsoleColor.DarkGreen);
            }
            PrettyConsole.Line("============", ConsoleColor.DarkGreen);
        }

        private static async Task ShowCurrentChannelHistory(IClusterClient client)
        {
            //var room = client.GetGrain<ILyraGossip>(joinedChannel);
            //var history = await room.ReadHistory(1000);

            //PrettyConsole.Line($"====== History for '{joinedChannel}' Channel ======", ConsoleColor.DarkGreen);
            //foreach (var chatMsg in history)
            //{
            //    PrettyConsole.Line($" ({chatMsg.Created:g}) {chatMsg.From}> {chatMsg.Text}", ConsoleColor.DarkGreen);
            //}
            //PrettyConsole.Line("============", ConsoleColor.DarkGreen);
        }

        private static async Task SendMessage(IClusterClient client, string messageText)
        {
            var room = client.GetGrain<ILyraGossip>(joinedChannel);
            await room.Message(new ChatMsg(userName, messageText));
        }

        private static async Task JoinChannel(IClusterClient client, string channelName)
        {
            //if (joinedChannel == channelName)
            //{
            //    PrettyConsole.Line($"You already joined channel {channelName}. Double joining a channel, which is implemented as a stream, would result in double subscription to the same stream, " +
            //                       $"which would result in receiving duplicated messages. For more information, please refer to Orleans streaming documentation.");
            //    return;
            //}
            //PrettyConsole.Line($"Joining to channel {channelName}");
            //joinedChannel = channelName;
            //var room = client.GetGrain<ILyraGossip>(joinedChannel);
            //var streamId = await room.Join(userName);
            //var stream = client.GetStreamProvider(LyraGossipConstants.LyraGossipStreamProvider)
            //    .GetStream<ChatMsg>(streamId, LyraGossipConstants.LyraGossipStreamNameSpace);
            ////subscribe to the stream to receiver furthur messages sent to the chatroom
            //await stream.SubscribeAsync(new StreamObserver(client.ServiceProvider.GetService<ILoggerFactory>()
            //    .CreateLogger($"{joinedChannel} channel")));
        }

        private static async Task LeaveChannel(IClusterClient client)
        {
            PrettyConsole.Line($"Leaving channel {joinedChannel}");
            var room = client.GetGrain<ILyraGossip>(joinedChannel);
            var streamId = await room.Leave(userName);
            var stream = client.GetStreamProvider(LyraGossipConstants.LyraGossipStreamProvider)
                .GetStream<ChatMsg>(streamId, LyraGossipConstants.LyraGossipStreamNameSpace);

            //unsubscribe from the channel/stream since client left, so that client won't
            //receive furture messages from this channel/stream
            var subscriptionHandles = await stream.GetAllSubscriptionHandles();
            foreach (var handle in subscriptionHandles)
            {
                await handle.UnsubscribeAsync();
            }
        }
    }
}
