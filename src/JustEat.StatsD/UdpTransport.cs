using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using JustEat.StatsD.EndpointLookups;

namespace JustEat.StatsD
{
    public class UdpTransport : IStatsDTransport
    {
        private static readonly SimpleObjectPool<SocketAsyncEventArgs> EventArgsPool
            = new SimpleObjectPool<SocketAsyncEventArgs>(30, pool => new PoolAwareSocketAsyncEventArgs(pool));

        private readonly IPEndPointSource _endpointSource;

        public UdpTransport(IPEndPointSource endPointSource)
        {
            _endpointSource = endPointSource ?? throw new ArgumentNullException(nameof(endPointSource));
        }

        public void Send(string metric)
        {
            Send(new[] {metric});
        }

        public void Send(IEnumerable<string> metrics)
        {
            var data = EventArgsPool.Pop();
            //firehose alert! -- keep it moving!
            if (data == null)
            {
                return;
            }

            data.RemoteEndPoint = _endpointSource.GetEndpoint();
            data.SendPacketsElements = metrics.ToMaximumBytePackets()
                .Select(bytes => new SendPacketsElement(bytes, 0, bytes.Length, true))
                .ToArray();

            using (var udpClient = GetUdpClient())
            {
                udpClient.Client.Connect(data.RemoteEndPoint);
                udpClient.Client.SendPacketsAsync(data);
            }
        }

        public UdpClient GetUdpClient()
        {
            return new UdpClient
            {
                Client = { SendBufferSize = 0 }
            };
        }
    }
}
