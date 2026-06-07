using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TrafficMonitor.Services
{
    #region ================== SPEED TEST RESULT CLASS ==================

    public class SpeedTestResult
    {
        public double DownloadMbps { get; set; }
        public double UploadMbps { get; set; }
        public double PingMs { get; set; }
        public double JitterMs { get; set; }
        public double PacketLoss { get; set; }
        public string ISP { get; set; }
        public string ServerName { get; set; }
        public string ServerLocation { get; set; }
        public string PublicIP { get; set; }
        public string LocalIP { get; set; }
        public DateTime TestTime { get; set; }
        public string ConnectionType { get; set; }
        public string ProviderUsed { get; set; }
        public string SelectedServer { get; set; }
        public string ServerCountry { get; set; }
        public string ServerRegion { get; set; }
        public double ServerLatency { get; set; }
    }

    #endregion

    #region ================== SPEED TEST PHASE ENUM ==================

    public enum SpeedTestPhase
    {
        Idle, Ping, Download, Upload, Done, Error
    }

    #endregion

    public class SpeedTestService : IDisposable
    {
        #region ================== CONSTANTS ==================

        private const string IPINFO_URL = "https://ipinfo.io/json";

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private HttpClient _client;
        private CancellationTokenSource _cts;
        private readonly SpeedTestManager _manager;
        private bool _disposed = false;

        #endregion

        #region ================== EVENTS ==================

        public event Action<SpeedTestPhase, double, string> ProgressChanged;

        #endregion

        #region ================== CONSTRUCTOR ==================

        public SpeedTestService()
        {
            _client = new HttpClient();
            _client.Timeout = TimeSpan.FromSeconds(30);
            _client.DefaultRequestHeaders.Add("User-Agent", "TrafficMonitor/1.0 SpeedTest");
            _manager = new SpeedTestManager();
            _cts = new CancellationTokenSource();
        }

        #endregion

        #region ================== MAIN TEST METHOD ==================

        public async Task<SpeedTestResult> RunFullTestAsync(CancellationToken cancellationToken = default)
        {
            var result = new SpeedTestResult
            {
                TestTime = DateTime.Now
            };

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
            var token = linkedCts.Token;

            try
            {
                ProgressChanged?.Invoke(SpeedTestPhase.Ping, 0, "SpeedTest_FindingServer");
                var provider = await _manager.SelectBestProviderAsync(token);
                result.ProviderUsed = provider.Name;

                if (provider is CloudflareProvider cfProvider)
                {
                    var serverInfo = cfProvider.GetSelectedServerLocation();
                    result.SelectedServer = serverInfo;
                    result.ServerName = serverInfo;
                }

                ProgressChanged?.Invoke(SpeedTestPhase.Ping, 5, "SpeedTest_GettingNetworkInfo");
                await GetNetworkInfoAsync(result, token);

                ProgressChanged?.Invoke(SpeedTestPhase.Ping, 15, "SpeedTest_TestingLatency");
                var pingResult = await provider.TestPingAsync(token);
                result.PingMs = pingResult.ping;
                result.JitterMs = pingResult.jitter;

                ProgressChanged?.Invoke(SpeedTestPhase.Ping, 30, "SpeedTest_TestingPacketLoss");
                result.PacketLoss = await provider.TestPacketLossAsync(token);

                ProgressChanged?.Invoke(SpeedTestPhase.Download, 40, "SpeedTest_TestingDownload");
                result.DownloadMbps = await provider.TestDownloadAsync(
                    token,
                    (speed, bytes) => ProgressChanged?.Invoke(
                        SpeedTestPhase.Download,
                        40 + (speed / 100) * 30,
                        "SpeedTest_TestingDownload"));

                ProgressChanged?.Invoke(SpeedTestPhase.Upload, 70, "SpeedTest_TestingUpload");
                result.UploadMbps = await provider.TestUploadAsync(
                    token,
                    (speed, bytes) => ProgressChanged?.Invoke(
                        SpeedTestPhase.Upload,
                        70 + (speed / 100) * 28,
                        "SpeedTest_TestingUpload"));

                ProgressChanged?.Invoke(SpeedTestPhase.Done, 100, "SpeedTest_Complete");
                return result;
            }
            catch (OperationCanceledException)
            {
                ProgressChanged?.Invoke(SpeedTestPhase.Error, 0, "SpeedTest_Cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SpeedTest error: {ex.Message}");
                ProgressChanged?.Invoke(SpeedTestPhase.Error, 0, $"SpeedTest_ErrorPrefix|{ex.Message}");
                throw;
            }
        }

        #endregion

        #region ================== NETWORK INFO ==================

        private async Task GetNetworkInfoAsync(SpeedTestResult result, CancellationToken ct)
        {
            try
            {
                var response = await _client.GetStringAsync(IPINFO_URL);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                result.PublicIP = root.TryGetProperty("ip", out var ip) ? ip.GetString() : "N/A";
                result.ISP = root.TryGetProperty("org", out var org) ? org.GetString()?.Replace("AS", "").Trim() : "Unknown";
                result.ServerLocation = root.TryGetProperty("city", out var city) ? $"{city.GetString()}, {(root.TryGetProperty("country", out var c) ? c.GetString() : "")}" : "Unknown";
                result.ServerName = result.ProviderUsed == "Cloudflare" ? "Cloudflare Speed Test" : $"{result.ProviderUsed} Speed Test";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetNetworkInfo error: {ex.Message}");
                result.PublicIP = "N/A";
                result.ISP = "Unknown";
                result.ServerLocation = "Unknown";
            }

            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                            !addr.Address.ToString().StartsWith("127"))
                        {
                            result.LocalIP = addr.Address.ToString();
                            result.ConnectionType = nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? "Wi-Fi" : "Ethernet";
                            break;
                        }
                    }
                    if (result.LocalIP != null) break;
                }
            }
            catch { }
        }

        #endregion

        #region ================== PUBLIC METHODS ==================

        public void Cancel()
        {
            _cts?.Cancel();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts?.Cancel();
            _cts?.Dispose();
            _client?.Dispose();
        }

        #endregion
    }
}