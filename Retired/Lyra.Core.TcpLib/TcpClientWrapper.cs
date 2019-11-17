using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TcpHelperLib
{
    public class TcpClientWrapper : TcpHelperBase
    {
        #region Vars & Props

        byte delim;
        int delimRepeated;

        public TcpClient Peer { get; internal set; }

        public string RemoteEndPoint
        {
            get { return Peer != null ? $"{Peer.Client.RemoteEndPoint}" : null; }
        }

        public bool IsConnected
        {
            get { return Peer != null && Peer.Connected; }
        }

        #endregion // Vars & Props

        #region Ctor

        public TcpClientWrapper(byte delim, int delimRepeated)
        {
            this.delim = delim;
            this.delimRepeated = delimRepeated;
        }

        #endregion // Ctor

        #region SendAsync

        public Task SendAsync(byte[] dataToSend = null)
        {
            Task task = null;
            if (Peer != null && Peer.Connected)
            {
                var lstByte = new List<byte>();

                if (dataToSend != null && dataToSend.Length > 0)
                {
                    lstByte.AddRange(BitConverter.GetBytes(DateTime.Now.Ticks)); // append timestamp prefix
                    lstByte.AddRange(dataToSend); // append payload - meaningful data
                }

                // Append delimiter
                for (int i = 0; i < delimRepeated; i++)
                    lstByte.Add(delim);

                var arrByte = lstByte.ToArray();

                try
                {
                    task = Peer.GetStream().WriteAsync(arrByte, 0, arrByte.Length);
                    SetLastInteractionTime();
                }
                catch (Exception e)
                {
                    LogError("TcpClientWrapper: SendAsync() failed.", e);
                }
            }

            return task;
        }

        public Task SendAsync(string s)
        {
            Task task = null;
            if (!string.IsNullOrEmpty(s))
                task = SendAsync(s.ToBytes());

            return task;
        }

        #endregion SendAsync
    }
}
