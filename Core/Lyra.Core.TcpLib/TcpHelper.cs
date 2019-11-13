using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TcpHelperLib
{
    public class TcpHelper : TcpHelperBase
    {
        #region Const 

        const int maxConnectionAttempts_default = 15;
        const int intervalBetweenConnectionAttemptsInMs_default = 1000;
        const int receiveBufferSize_default = 1024;
        const int logLevel_default = -1; 
        const char delim_default = '|';
        const int delimRepeated_default = 3;
        public const string ackConnection_default = "is connected to node";

        #endregion // Const 

        #region Vars & Props

        readonly int maxConnectionAttempts;
        readonly int intervalBetweenConnectionAttemptsInMs;
        readonly int receiveBufferSize;
        public byte Delim { get; private set; }
        public int DelimRepeated { get; private set; }
        readonly string ackConnection;

        bool isActive = false;

        public string Id { get; protected set; }

        CancellationTokenSource cts = new CancellationTokenSource();

        Func<DateTime, List<byte>, TcpClientWrapper, StateProperties, ProcessingResult> processMethod;

        #endregion // Vars & Props

        #region Ctors

        public TcpHelper(string id,
                         Func<DateTime, List<byte>, TcpClientWrapper, StateProperties, ProcessingResult> processMethod,
                         int maxConnectionAttempts = maxConnectionAttempts_default,
                         int intervalBetweenConnectionAttemptsInMs = intervalBetweenConnectionAttemptsInMs_default,
                         int receiveBufferSize = receiveBufferSize_default,
                         int logLevel = logLevel_default,
                         char delim = delim_default,
                         int delimRepeated = delimRepeated_default,
                         string ackConnection = ackConnection_default)
        {
            Id = string.IsNullOrEmpty(id) ? $"{Guid.NewGuid()}" : id;
            this.processMethod = processMethod;

            this.maxConnectionAttempts = maxConnectionAttempts;
            this.intervalBetweenConnectionAttemptsInMs = intervalBetweenConnectionAttemptsInMs;
            this.receiveBufferSize = receiveBufferSize;
            TcpHelperBase.logLevel = logLevel;
            Delim = (byte)delim;
            DelimRepeated = delimRepeated;
            this.ackConnection = ackConnection;

            //1  this.receiveBufferSize = 29; // short buffer test
        }

        public TcpHelper(string id,
                         Func<DateTime, List<byte>, TcpClientWrapper, StateProperties, ProcessingResult> processMethod,
                         string configFilePath)
        {
            Id = string.IsNullOrEmpty(id) ? $"{Guid.NewGuid()}" : id;
            this.processMethod = processMethod;

            var config = new ConfigurationBuilder().AddJsonFile(configFilePath, optional: true, reloadOnChange: false).Build();
            var configSection = config.GetSection("TcpHelper");
            maxConnectionAttempts = configSection.GetValue<int>("maxConnectionAttempts", maxConnectionAttempts_default);
            intervalBetweenConnectionAttemptsInMs = configSection.GetValue<int>("intervalBetweenConnectionAttemptsInMs",
                                                                intervalBetweenConnectionAttemptsInMs_default);
            receiveBufferSize = configSection.GetValue("receiveBufferSize", receiveBufferSize_default);
            logLevel = configSection.GetValue("logLevel", logLevel_default);
            Delim = (byte)configSection.GetValue("delim", delim_default);
            DelimRepeated = configSection.GetValue("delimRepeated", delimRepeated_default);
            ackConnection = configSection.GetValue("ackConnection", ackConnection_default);

            //1  this.receiveBufferSize = 29;  // short buffer test
        }

        #endregion // Ctors

        #region SERVER Specific

        public async void Listen(int port, string host = null)
        {
            var listener = new TcpListener(GetHost(ref host), port);
            listener.Start();

            // Continue listening.  
            Log($"Server \"{Id}\" is listening on {host}:{port} ...");
            TcpClient client = null;
            while ((client = await listener.AcceptTcpClientAsync()) != null && client.Connected)
            {
                var str = $"Client {client.Client.RemoteEndPoint} {ackConnection} \"{Id}\" {host}:{port}";
                Log(str);
                isActive = true;
                var clientWrapper = new TcpClientWrapper(Delim, DelimRepeated) { Peer = client };
                await clientWrapper.SendAsync();
                await clientWrapper.SendAsync(str);
                Receive(client);
            }
        }

        #endregion // SERVER Specific

        #region CLIENT Specific

        public async Task<TcpClientWrapper> Connect(int port, string host = null)
        {
            var server = await GetConnectedServer(host, port);

            if (server.Connected)
            {
                Log($"Client \"{Id}\" connected to Server {host}:{port}.");
                Receive(server);
            }
            else
                LogError("Connection failed after reties.");

#if DEBUG
            server.ReceiveTimeout = server.SendTimeout = 30 * 60 * 1000;
#endif
            return new TcpClientWrapper(Delim, DelimRepeated) { Peer = server };
        }

        private async Task<TcpClient> GetConnectedServer(string host, int port)
        {
            var server = new TcpClient();
            for (int i = 0; i < maxConnectionAttempts && !server.Connected && !cts.IsCancellationRequested; i++)
            {
                try
                {
                    await server.Client.ConnectAsync(GetHost(ref host), port);
                }
                catch
                {
                    await Task.Delay(intervalBetweenConnectionAttemptsInMs, cts.Token);
                }
            }

            return server;
        }

        #endregion // CLIENT Specific

        #region Process Received

        private async void Receive(TcpClient client)
        {
            if (client == null || !client.Connected)
                return;

            var lstData = new List<byte>();
            var stateProprties = new StateProperties();

            using (var netStream = client.GetStream())
            {
                var clientWrapper = new TcpClientWrapper(Delim, DelimRepeated) { Peer = client };
                var buffer = new byte[receiveBufferSize];
                int readBytes = 0;
                while ((readBytes = await ReadNetStreamAsync(netStream, buffer, cts.Token)) > 0)
                {
                    lstData.AddRange(GetReceivedBuffer(buffer, readBytes));

                    try
                    {
                        ProcessReceived(lstData, clientWrapper, stateProprties);
                        SetLastInteractionTime();
                    }
                    catch (Exception e)
                    {
                        LogError("ProcessReceived() failed.", e);
                    }
                }
            }
        }

        private async void ProcessReceived(List<byte> lstData, TcpClientWrapper clientWrapper, StateProperties stateProprties)
        {
            Log($"{clientWrapper.RemoteEndPoint}  ThreadId = {Thread.CurrentThread.ManagedThreadId}");

            // Split to parts by delimiter and update lstData
            var lstParts = SplitToDelimitedParts(lstData);

            // Process parts 
            foreach (var lstByte in lstParts)
            {
                ProcessingResult result = null;
                if (lstByte != null)
                {
                    var timestamp = GetTimestamp(lstByte);
                    if (timestamp > DateTime.MinValue)
                    {
                        CheckIfRpc(lstByte, stateProprties);
                        try
                        {
                            result = processMethod?.Invoke(timestamp, lstByte, clientWrapper, stateProprties);
                            //result = res.Result;
                        }
                        catch (Exception e)
                        {
                            LogError("ProcessReceived(): user supplied processMethod() callback failed.", e);
                        }
                    }
                    else
                        LogError("ProcessReceived(): timestamp == DateTime.MinValue");
                }

                if (result != null)
                {
                    byte[] bts = result.BytesToSendBack;
                    if (bts != null && bts.Length > 0)
                    {
                        var task = clientWrapper.SendAsync(bts);

                        if (result.IsSyncSend)
                            await task;
                    }
                }
            }
        }

        #endregion // Process Received

        #region Aux - private

        private List<List<byte>> SplitToDelimitedParts(List<byte> lstData)
        {
            var lstOuter = new List<List<byte>>();
            var lstInner = new List<byte>();
            int delimCount = 0;

            foreach (byte bt in lstData)
            {
                if (bt == Delim)
                {
                    if (++delimCount == DelimRepeated)
                    {
                        isActive = true;

                        if (lstInner.Count > 0)
                        {
                            // New delimited part
                            lstOuter.Add(new List<byte>(lstInner));
                            lstInner.Clear();
                        }

                        delimCount = 0;
                    }
                }
                else
                {
                    AddDelimsToInnerList(lstInner, isActive, ref delimCount);
                    
                    if (isActive)
                        lstInner.Add(bt);
                }
            }

            AddDelimsToInnerList(lstInner, isActive, ref delimCount);

            lstData.Clear();
            if (lstInner.Count > 0)
                lstData.AddRange(lstInner); //1*

            return lstOuter;
        }

        private void AddDelimsToInnerList(List<byte> lstInner, bool isActive, ref int delimCount)
        {
            if (isActive && delimCount > 0)
            {
                for (int i = 0; i < delimCount; i++)
                    lstInner.Add(Delim);

                delimCount = 0;
            }
        }

        private async Task<int> ReadNetStreamAsync(NetworkStream netStream, byte[] buffer, CancellationToken token)
        {
            try
            {
                return await netStream.ReadAsync(buffer, 0, buffer.Length, token);
            }
            catch (Exception e)
            {
                LogError("ReadNetStreamAsync() failed.", e);
                return 0;
            }
        }

        private static byte[] GetReceivedBuffer(byte[] buffer, int readBytes)
        {
            byte[] bufReceived;
            if (readBytes < buffer.Length)
            {
                bufReceived = new byte[readBytes];
                Array.Copy(buffer, bufReceived, readBytes);
            }
            else
                bufReceived = buffer;

            return bufReceived;
        }

        private DateTime GetTimestamp(List<byte> lstByte)
        {
            DateTime dt = DateTime.MinValue;
            var prefixSize = sizeof(long); 
            if (lstByte != null && lstByte.Count >= prefixSize)
            {
                var bts = new byte[prefixSize];
                lstByte.CopyTo(0, bts, 0, bts.Length);

                try
                {
                    dt = new DateTime(BitConverter.ToInt64(bts, 0));
                    lstByte.RemoveRange(0, bts.Length);
                }
                catch (Exception e)
                {
                    LogError("GetTimestamp() failed.", e);
                }
            }

            return dt;
        }

        private static void CheckIfRpc(List<byte> lstByte, StateProperties stateProprties)
        {
            var rpi = GetRpi(lstByte);
            if (rpi != null && !string.IsNullOrEmpty(rpi.Name))
                stateProprties[rpi.Name] = rpi;
        }

        private static RemoteProcInfo GetRpi(List<byte> lstByte)
        {
            RemoteProcInfo rpi = null;

            try
            {
                rpi = RemoteProcInfo.FromJson(lstByte.ToStr());
            }
            catch (Exception e)
            {
               // LogError("GetRpi() failed.", e);
            }

            return rpi;
        }

        private static IPAddress GetHost(ref string host)
        {
            return IPAddress.Parse(host = string.IsNullOrEmpty(host) ? "127.0.0.1" : host);
        }

        #endregion // Aux - private

        #region Stop

        public void Stop()
        {
            cts.Cancel();
        }

        #endregion // Stop
    }

    public class ProcessingResult
    {
        private byte[] bytesToSendBack;
        public byte[] BytesToSendBack
        {
            get
            {
                if (bytesToSendBack != null && bytesToSendBack.Length > 0)
                    return bytesToSendBack;

                if (!string.IsNullOrEmpty(StringToSendBack))
                    return StringToSendBack.ToBytes();

                return null;
            }
            set
            {
                bytesToSendBack = value;
            }
        }

        public string StringToSendBack { get; set; }
        public bool IsSyncSend { get; set; } = false;

    }
}
