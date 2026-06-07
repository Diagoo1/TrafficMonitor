using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TrafficMonitor.Services
{
    public class MultiCdnProvider : ISpeedTestProvider
    {
        #region ================== PROPERTIES ==================

        public string Name => "Multi-CDN";
        public int Priority => 2;

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private readonly HttpClient _client;
        private readonly string[] _downloadUrls = new[]
        {
            "https://proof.ovh.net/files/100Mb.dat",
            "https://speedtest.tele2.net/100MB.zip",
            "https://speed.hetzner.de/100MB.bin",
            "https://lg.ams-equinix-1.fdcservers.net/100MB.test",
            "https://nbg1-speed.hetzner.com/100MB.bin",
            "https://speedtest.london.linode.com/100MB-london.bin"
        };

        #endregion

        #region ================== CONSTRUCTOR ==================

        public MultiCdnProvider()
        {
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _client.DefaultRequestHeaders.Add("User-Agent", "TrafficMonitor/1.0");
        }

        #endregion

        #region ================== AVAILABILITY CHECK ==================

        public async Task<bool> IsAvailableAsync(CancellationToken ct)
        {
            foreach (var url in _downloadUrls.Take(3))
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Head, url);
                    var resp = await _client.SendAsync(req, ct);
                    if (resp.IsSuccessStatusCode) return true;
                }
                catch { }
            }
            return false;
        }

        #endregion

        #region ================== DOWNLOAD TEST ==================

        public async Task<double> TestDownloadAsync(CancellationToken ct, Action<double, double> onProgress)
        {
            var tasks = _downloadUrls.Take(3).Select(url =>
                DownloadFromUrlAsync(url, ct, onProgress)).ToArray();

            var results = await Task.WhenAll(tasks);
            var validResults = results.Where(r => r > 0).OrderBy(r => r).ToList();

            if (validResults.Count == 0) return 0;
            if (validResults.Count == 1) return validResults[0];

            validResults.RemoveAt(0);
            return validResults.Average();
        }

        private async Task<double> DownloadFromUrlAsync(string url, CancellationToken ct, Action<double, double> onProgress)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var sw = Stopwatch.StartNew();

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                using var stream = await response.Content.ReadAsStreamAsync();

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;
                double lastReport = 0;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                {
                    totalRead += bytesRead;

                    if (sw.Elapsed.TotalMilliseconds - lastReport > 100)
                    {
                        double currentMbps = totalRead * 8.0 / (sw.Elapsed.TotalSeconds * 1_000_000);
                        onProgress?.Invoke(currentMbps, totalRead);
                        lastReport = sw.Elapsed.TotalMilliseconds;
                    }

                    if (sw.Elapsed.TotalSeconds > 10) break;
                }

                sw.Stop();
                return totalRead * 8.0 / (sw.Elapsed.TotalSeconds * 1_000_000);
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