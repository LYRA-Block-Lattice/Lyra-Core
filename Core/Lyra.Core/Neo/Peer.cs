using Akka.Actor;
using Akka.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Neo.IO;
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Lyra.Data.Utils;

namespace Neo.Network.P2P
{
    public abstract class Peer : UntypedActor
    {
        public class Peers { public IEnumerable<IPEndPoint> EndPoints; }
        public class Connect { public IPEndPoint EndPoint; public bool IsTrusted = false; }
        private class Timer { }
        private class WsConnected { public WebSocket Socket; public IPEndPoint Remote; public IPEndPoint Local; }

        // big. we need a full connected network (at least for now.)
        public const int DefaultMinDesiredConnections = 30;
        public const int DefaultMaxConnections = DefaultMinDesiredConnections * 4;

        private static readonly IActorRef tcp_manager = Context.System.Tcp();
        private IActorRef tcp_listener;
        private IWebHost ws_host;
        private ICancelable timer;
        protected ActorSelection Connections => Context.ActorSelection("connection_*");

        private static readonly HashSet<IPAddress> localAddresses = new HashSet<IPAddress>();
        private readonly Dictionary<IPAddress, int> ConnectedAddresses = new Dictionary<IPAddress, int>();
        /// <summary>
        /// A dictionary that stores the connected nodes.
        /// </summary>
        protected readonly ConcurrentDictionary<IActorRef, IPEndPoint> ConnectedPeers = new ConcurrentDictionary<IActorRef, IPEndPoint>();
        /// <summary>
        /// An ImmutableHashSet that stores the Peers received: 1) from other nodes or 2) from default file.
        /// If the number of desired connections is not enough, first try to connect with the peers from this set.
        /// </summary>
        protected ImmutableHashSet<IPEndPoint> UnconnectedPeers = ImmutableHashSet<IPEndPoint>.Empty;
        /// <summary>
        /// When a TCP connection request is sent to a peer, the peer will be added to the ImmutableHashSet.
        /// If a Tcp.Connected or a Tcp.CommandFailed (with TCP.Command of type Tcp.Connect) is received, the related peer will be removed.
        /// </summary>
        protected ImmutableHashSet<IPEndPoint> ConnectingPeers = ImmutableHashSet<IPEndPoint>.Empty;
        protected HashSet<IPAddress> TrustedIpAddresses { get; } = new HashSet<IPAddress>();

        public int ListenerTcpPort { get; private set; }
        public int ListenerWsPort { get; private set; }
        public int MaxConnectionsPerAddress { get; private set; } = 3;
        public int MinDesiredConnections { get; private set; } = DefaultMinDesiredConnections;
        public int MaxConnections { get; private set; } = DefaultMaxConnections;
        protected int UnconnectedMax { get; } = 1000;
        protected virtual int ConnectingMax
        {
            get
            {
                var allowedConnecting = MinDesiredConnections * 4;
                allowedConnecting = MaxConnections != -1 && allowedConnecting > MaxConnections
                    ? MaxConnections : allowedConnecting;
                return allowedConnecting - ConnectedPeers.Count;
            }
        }

        static Peer()
        {
            localAddresses.UnionWith(NetworkInterface.GetAllNetworkInterfaces().SelectMany(p => p.GetIPProperties().UnicastAddresses).Select(p => p.Address.Unmap()));
        }

        /// <summary>
        /// Tries to add a set of peers to the immutable ImmutableHashSet of UnconnectedPeers.
        /// </summary>
        /// <param name="peers">Peers that the method will try to add (union) to (with) UnconnectedPeers.</param>
        protected void AddPeers(IEnumerable<IPEndPoint> peers)
        {
            if (UnconnectedPeers.Count < UnconnectedMax)
            {
                // Do not select peers to be added that are already on the ConnectedPeers
                // If the address is the same, the ListenerTcpPort should be different
                peers = peers.Where(p => (p.Port != ListenerTcpPort || !localAddresses.Contains(p.Address)) && !ConnectedPeers.Values.Contains(p));
                ImmutableInterlocked.Update(ref UnconnectedPeers, p => p.Union(peers));
            }
        }

        protected void ConnectToPeer(IPEndPoint endPoint, bool isTrusted = false)
        {
            endPoint = endPoint.Unmap();
            // If the address is the same, the ListenerTcpPort should be different, otherwise, return
            if (endPoint.Port == ListenerTcpPort && localAddresses.Contains(endPoint.Address)) return;

            if (isTrusted) TrustedIpAddresses.Add(endPoint.Address);
            // If connections with the peer greater than or equal to MaxConnectionsPerAddress, return.
            if (ConnectedAddresses.TryGetValue(endPoint.Address, out int count) && count >= MaxConnectionsPerAddress)
                return;
            if (ConnectedPeers.Values.Contains(endPoint)) return;
            ImmutableInterlocked.Update(ref ConnectingPeers, p =>
            {
                if ((p.Count >= ConnectingMax && !isTrusted) || p.Contains(endPoint)) return p;
                tcp_manager.Tell(new Tcp.Connect(endPoint));
                return p.Add(endPoint);
            });
        }

        private static bool IsIntranetAddress(IPAddress address)
        {
            byte[] data = address.MapToIPv4().GetAddressBytes();
            uint value = BinaryPrimitives.ReadUInt32BigEndian(data);
            return (value & 0xff000000) == 0x0a000000 || (value & 0xff000000) == 0x7f000000 || (value & 0xfff00000) == 0xac100000 || (value & 0xffff0000) == 0xc0a80000 || (value & 0xffff0000) == 0xa9fe0000;
        }

        /// <summary>
        /// Abstract method for asking for more peers. Currently triggered when UnconnectedPeers is empty.
        /// </summary>
        /// <param name="count">Number of peers that are being requested.</param>
        protected abstract void NeedMorePeers(int count);

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case ChannelsConfig config:
                    OnStart(config);
                    break;
                case Timer _:
                    OnTimer();
                    break;
                case Peers peers:
                    AddPeers(peers.EndPoints);
                    break;
                case Connect connect:
                    ConnectToPeer(connect.EndPoint, connect.IsTrusted);
                    break;
                case WsConnected ws:
                    OnWsConnected(ws.Socket, ws.Remote, ws.Local);
                    break;
                case Tcp.Connected connected:
                    OnTcpConnected(((IPEndPoint)connected.RemoteAddress).Unmap(), ((IPEndPoint)connected.LocalAddress).Unmap());
                    break;
                case Tcp.Bound _:
                    tcp_listener = Sender;
                    break;
                case Tcp.CommandFailed commandFailed:
                    OnTcpCommandFailed(commandFailed.Cmd);
                    break;
                case Terminated terminated:
                    OnTerminated(terminated.ActorRef);
                    break;
            }
        }

        private class BeforeBind : Inet.SocketOptionV2
        {
            public override void BeforeServerSocketBind(Socket ss)
            {
                ss.DualMode = true;
                base.BeforeServerSocketBind(ss);
            }
        }

        private void OnStart(ChannelsConfig config)
        {
            ListenerTcpPort = config.Tcp?.Port ?? 0;
            ListenerWsPort = config.WebSocket?.Port ?? 0;

            MinDesiredConnections = config.MinDesiredConnections;
            MaxConnections = config.MaxConnections;
            MaxConnectionsPerAddress = config.MaxConnectionsPerAddress;

            // schedule time to trigger `OnTimer` event every TimerMillisecondsInterval ms
            timer = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(0, 5000, Context.Self, new Timer(), ActorRefs.NoSender);
            if ((ListenerTcpPort > 0 || ListenerWsPort > 0)
                && localAddresses.All(p => !p.IsIPv4MappedToIPv6 || IsIntranetAddress(p))
                && UPnP.Discover())
            {
                try
                {
                    localAddresses.Add(UPnP.GetExternalIP());

                    if (ListenerTcpPort > 0) UPnP.ForwardPort(ListenerTcpPort, ProtocolType.Tcp, "NEO Tcp");
                    if (ListenerWsPort > 0) UPnP.ForwardPort(ListenerWsPort, ProtocolType.Tcp, "NEO WebSocket");
                }
                catch { }
            }
            if (ListenerTcpPort > 0)
            {
                tcp_manager.Tell(new Tcp.Bind(Self, config.Tcp, options: new Inet.SocketOption [] { new Inet.SO.ReuseAddress(true), new BeforeBind() }));
            }
            //if (ListenerWsPort > 0)
            //{
            //    var host = "*";

            //    if (!config.WebSocket.Address.GetAddressBytes().SequenceEqual(IPAddress.Any.GetAddressBytes()))
            //    {
            //        // Is not for all interfaces
            //        host = config.WebSocket.Address.ToString();
            //    }

            //    ws_host = new WebHostBuilder().UseKestrel().UseUrls($"http://{host}:{ListenerWsPort}").Configure(app => app.UseWebSockets().Run(ProcessWebSocketAsync)).Build();
            //    ws_host.Start();
            //}
        }

        /// <summary>
        /// Will be triggered when a Tcp.Connected message is received.
        /// If the conditions are met, the remote endpoint will be added to ConnectedPeers.
        /// Increase the connection number with the remote endpoint by one.
        /// </summary>
        /// <param name="remote">The remote endpoint of TCP connection.</param>
        /// <param name="local">The local endpoint of TCP connection.</param>
        private void OnTcpConnected(IPEndPoint remote, IPEndPoint local)
        {
            ImmutableInterlocked.Update(ref ConnectingPeers, p => p.Remove(remote));
            if (MaxConnections != -1 && ConnectedPeers.Count >= MaxConnections && !TrustedIpAddresses.Contains(remote.Address))
            {
                Sender.Tell(Tcp.Abort.Instance);
                return;
            }

            ConnectedAddresses.TryGetValue(remote.Address, out int count);
            if (count >= MaxConnectionsPerAddress)
            {
                Sender.Tell(Tcp.Abort.Instance);
            }
            else
            {
                ConnectedAddresses[remote.Address] = count + 1;
                IActorRef connection = Context.ActorOf(ProtocolProps(Sender, remote, local), $"connection_{Guid.NewGuid()}");
                Context.Watch(connection);
                Sender.Tell(new Tcp.Register(connection));
                ConnectedPeers.TryAdd(connection, remote);
                OnTcpConnected(connection);
            }
        }

        protected virtual void OnTcpConnected(IActorRef connection)
        {
        }

        /// <summary>
        /// Will be triggered when a Tcp.CommandFailed message is received.
        /// If it's a Tcp.Connect command, remove the related endpoint from ConnectingPeers.
        /// </summary>
        /// <param name="cmd">Tcp.Command message/event.</param>
        private void OnTcpCommandFailed(Tcp.Command cmd)
        {
            switch (cmd)
            {
                case Tcp.Connect connect:
                    ImmutableInterlocked.Update(ref ConnectingPeers, p => p.Remove(((IPEndPoint)connect.RemoteAddress).Unmap()));
                    break;
            }
        }

        private void OnTerminated(IActorRef actorRef)
        {
            if (ConnectedPeers.TryRemove(actorRef, out IPEndPoint endPoint))
            {
                ConnectedAddresses.TryGetValue(endPoint.Address, out int count);
                if (count > 0) count--;
                if (count == 0)
                    ConnectedAddresses.Remove(endPoint.Address);
                else
                    ConnectedAddresses[endPoint.Address] = count;
            }
        }

        private void OnTimer()
        {
            // Check if the number of desired connections is already enough
            if (ConnectedPeers.Count >= MinDesiredConnections) return;

            // If there aren't available UnconnectedPeers, it triggers an abstract implementation of NeedMorePeers 
            if (UnconnectedPeers.Count == 0)
                NeedMorePeers(MinDesiredConnections - ConnectedPeers.Count);
            IPEndPoint[] endpoints = UnconnectedPeers.Take(MinDesiredConnections - ConnectedPeers.Count).ToArray();
            ImmutableInterlocked.Update(ref UnconnectedPeers, p => p.Except(endpoints));
            foreach (IPEndPoint endpoint in endpoints)
            {
                ConnectToPeer(endpoint);
            }
        }

        private void OnWsConnected(WebSocket ws, IPEndPoint remote, IPEndPoint local)
        {
            ConnectedAddresses.TryGetValue(remote.Address, out int count);
            if (count >= MaxConnectionsPerAddress)
            {
                ws.Abort();
            }
            else
            {
                ConnectedAddresses[remote.Address] = count + 1;
                Context.ActorOf(ProtocolProps(ws, remote, local), $"connection_{Guid.NewGuid()}");
            }
        }

        protected override void PostStop()
        {
            timer.CancelIfNotNull();
            ws_host?.Dispose();
            tcp_listener?.Tell(Tcp.Unbind.Instance);
            base.PostStop();
        }

        private async Task ProcessWebSocketAsync(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest) return;
            WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();
            Self.Tell(new WsConnected
            {
                Socket = ws,
                Remote = new IPEndPoint(context.Connection.RemoteIpAddress, context.Connection.RemotePort),
                Local = new IPEndPoint(context.Connection.LocalIpAddress, context.Connection.LocalPort)
            });
        }

        protected abstract Props ProtocolProps(object connection, IPEndPoint remote, IPEndPoint local);
    }
}
