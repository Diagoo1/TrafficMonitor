using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TrafficMonitor.Services
{
    public class CloudflareProvider : ISpeedTestProvider
    {
        #region ================== PROPERTIES ==================

        public string Name => "Cloudflare";
        public int Priority => 1;

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private readonly HttpClient _client;
        private readonly GeoServerSelector _serverSelector;
        private ServerInfo _selectedServer;

        #endregion

        #region ================== CONSTRUCTOR ==================

        public CloudflareProvider()
        {
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _client.DefaultRequestHeaders.Add("User-Agent", "TrafficMonitor/1.0");
            _serverSelector = new GeoServerSelector();
        }

        #endregion

        #region ================== AVAILABILITY CHECK ==================

        public async Task<bool> IsAvailableAsync(CancellationToken ct)
        {
            try
            {
                _selectedServer = await _serverSelector.SelectBestServerAsync(ct);
                using var req = new HttpRequestMessage(HttpMethod.Head, $"{_selectedServer.BaseUrl}/__down?bytes=1000");
                var resp = await _client.SendAsync(req, ct);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public string GetSelectedServerLocation() => _selectedServer?.ToString() ?? "Unknown";

        #endregion

        #region ================== DOWNLOAD TEST ==================

        public async Task<double> TestDownloadAsync(CancellationToken ct, Action<double, double> onProgress)
        {
            var speeds = new List<double>();
            var sizes = new[] { 101000, 1001000, 10001000, 25001000 };

            foreach (int size in sizes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var url = $"{_selectedServer.BaseUrl}/__down?bytes={size}";
                    var sw = Stopwatch.StartNew();

                    using var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                    using var stream = await response.Content.ReadAsStreamAsync();

                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int bytesRead;

                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                    {
                        totalRead += bytesRead;
                        double progress = (double)totalRead / size * 100;
                        onProgress?.Invoke(progress, totalRead);
                    }

                    sw.Stop();
                    double mbps = totalRead * 8.0 / (sw.Elapsed.TotalSeconds * 1_000_000);
                    speeds.Add(mbps);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Download chunk error: {ex.Message}");
                }
            }

            if (speeds.Count == 0) return 0;
            if (speeds.Count == 1) return speeds[0];

            speeds.Sort();
            speeds.RemoveAt(0);
            return speeds.Average();
        }

        #endregion

        #region ================== UPLOAD TEST ==================

        public async Task<double> TestUploadAsync(CancellationToken ct, Action<double, double> onProgress)
        {
            var speeds = new List<double>();
            var sizes = new[] { 101000, 1001000, 10001000 };

            foreach (int size in sizes)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var data = new byte[size];
                    new Random().NextBytes(data);

                    var content = new ByteArrayContent(data);
                    var sw = Stopwatch.StartNew();

                    using var response = await _client.PostAsync($"{_selectedServer.BaseUrl}/__up", content, ct);
                    sw.Stop();

                    double mbps = size * 8.0 / (sw.Elapsed.TotalSeconds * 1_000_000);
                    speeds.Add(mbps);
                    onProgress?.Invoke((double)(sizes.ToList().IndexOf(size) + 1) / sizes.Length * 100, size);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Upload chunk error: {ex.Message}");
                }
            }

            if (speeds.Count == 0) return 0;
            speeds.Sort();
            if (speeds.Count > 1) speeds.RemoveAt(0);
            return speeds.Average();
        }

        #endregion

        #region ================== PING & PACKET LOSS ==================

        public async Task<(double ping, double jitter)> TestPingAsync(CancellationToken ct)
        {
            return await LocalFallbackProvider.TestPingAsyncInternal(ct);
        }

        public async Task<double> TestPacketLossAsync(CancellationToken ct)
        {
            return await LocalFallbackProvider.TestPacketLossAsyncInternal(ct);
        }

        #endregion
    }
}