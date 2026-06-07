using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace TrafficMonitor.Services
{
    public class LocalFallbackProvider : ISpeedTestProvider
    {
        #region ================== PROPERTIES ==================

        public string Name => "Local Estimation";
        public int Priority => 99;

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private readonly string[] _dnsServers = new[]
        {
            "8.8.8.8", "8.8.4.8",
            "1.1.1.1", "1.0.0.1",
            "9.9.9.9", "149.112.112.112",
            "208.67.222.222", "208.67.220.220"
        };

        #endregion

        #region ================== AVAILABILITY CHECK ==================

        public async Task<bool> IsAvailableAsync(CancellationToken ct)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 2000);
                return reply.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region ================== DOWNLOAD TEST ==================

        public async Task<double> TestDownloadAsync(CancellationToken ct, Action<double, double> onProgress)
        {
            var speeds = new List<double>();

            foreach (var server in _dnsServers.Take(4))
            {
                try
                {
                    var speed = await EstimateThroughputAsync(server, 53, ct);
                    if (speed > 0) speeds.Add(speed);
                    onProgress?.Invoke(speeds.Count > 0 ? speeds.Average() : 0, 0);
                }
                catch { }
            }

            return speeds.Count > 0 ? speeds.Average() : 0;
        }

        private async Task<double> EstimateThroughputAsync(string host, int port, CancellationToken ct)
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);

            var sw = Stopwatch.StartNew();

            try
            {
                await socket.ConnectAsync(host, port);
                var connectTime = sw.ElapsedMilliseconds;

                if (connectTime < 20) return 100;
                if (connectTime < 50) return 50;
                if (connectTime < 100) return 25;
                if (connectTime < 200) return 10;
                return 5;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region ================== UPLOAD TEST ==================

        public async Task<double> TestUploadAsync(CancellationToken ct, Action<double, double> onProgress)
        {
            var download = await TestDownloadAsync(ct, null);
            return download * 0.7;
        }

        #endregion

        #region ================== PING & PACKET LOSS ==================

        public static async Task<(double ping, double jitter)> TestPingAsyncInternal(CancellationToken ct)
        {
            var pings = new List<double>();
            using var pinger = new Ping();
            var servers = new[] { "8.8.8.8", "1.1.1.1", "9.9.9.9" };

            foreach (var server in servers)
            {
                try
                {
                    var reply = await pinger.SendPingAsync(server, 2000);
                    if (reply.Status == IPStatus.Success)
                        pings.Add(reply.RoundtripTime);
                    await Task.Delay(100, ct);
                }
                catch { }
            }

            if (pings.Count == 0) return (0, 0);

            double avgPing = pings.Min();
            double jitter = 0;
            for (int i = 1; i < pings.Count; i++)
                jitter += Math.Abs(pings[i] - pings[i - 1]);
            jitter = pings.Count > 1 ? jitter / (pings.Count - 1) : 0;

            return (avgPing, jitter);
        }

        public static async Task<double> TestPacketLossAsyncInternal(CancellationToken ct)
        {
            int sent = 10, received = 0;
            using var pinger = new Ping();

            for (int i = 0; i < sent; i++)
            {
                try
                {
                    var reply = await pinger.SendPingAsync("8.8.8.8", 1000);
                    if (reply.Status == IPStatus.Success) received++;
                    await Task.Delay(100, ct);
                }
                catch { }
            }

            return ((sent - received) / (double)sent) * 100;
        }

        public async Task<(double ping, double jitter)> TestPingAsync(CancellationToken ct)
        {
            return await TestPingAsyncInternal(ct);
        }

        public async Task<double> TestPacketLossAsync(CancellationToken ct)
        {
            return await TestPacketLossAsyncInternal(ct);
        }

        #endregion
    }
}