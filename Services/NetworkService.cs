using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Timers;
using System.Windows;
using System.Linq;
using TrafficMonitor.Models;
using Timer = System.Timers.Timer;
using ElapsedEventArgs = System.Timers.ElapsedEventArgs;

namespace TrafficMonitor.Services
{
    public class NetworkService : IDisposable
    {
        #region ================== PRIVATE FIELDS ==================

        private Timer timer;
        private Timer historySaveTimer;
        private double intervalSeconds = 1.0;
        private HistoryService historyService;

        private long currentInBytes = 0;
        private long currentOutBytes = 0;
        private long lastInBytes = 0;
        private long lastOutBytes = 0;

        private long todayUpload = 0;
        private long todayDownload = 0;
        private DateTime currentDate = DateTime.Today;

        private long lastSavedUpload = 0;
        private long lastSavedDownload = 0;

        private string currentAdapter = "";
        private string currentAdapterType = "Unknown";
        private int zeroSpeedCount = 0;

        private bool _trafficAlertFired = false;
        private DateTime _alertFiredDate = DateTime.MinValue;

        #endregion

        #region ================== EVENTS ==================

        public event Action<long, long> TrafficLimitReached;
        public event Action<NetworkData> DataUpdated;

        #endregion

        #region ================== CONSTRUCTOR ==================

        public NetworkService()
        {
            historyService = new HistoryService();
            currentDate = DateTime.Today;
            LoadTodayFromHistory();
            InitializeAdapters();
            Start();
            StartHistoryAutoSave();
        }

        #endregion

        #region ================== HISTORY HELPERS ==================

        private void LoadTodayFromHistory()
        {
            var history = historyService.GetHistory();
            var today = history.FirstOrDefault(h => h.Date.Date == DateTime.Today);
            if (today != null)
            {
                todayUpload = today.UploadBytes;
                todayDownload = today.DownloadBytes;
                lastSavedUpload = todayUpload;
                lastSavedDownload = todayDownload;
            }
        }

        public HistoryService GetHistoryService() => historyService;

        #endregion

        #region ================== ADAPTER HELPERS ==================

        public string GetCurrentAdapterType() => currentAdapterType;
        public string GetCurrentAdapterName() => currentAdapter;

        private void InitializeAdapters()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        if (currentAdapter == "")
                        {
                            currentAdapter = nic.Name;
                            currentAdapterType = GetAdapterTypeName(nic);
                        }
                    }
                }

                if (currentAdapter == "")
                {
                    currentAdapter = "Ethernet";
                    currentAdapterType = "Ethernet";
                }

                UpdateCurrentBytes();
                lastInBytes = currentInBytes;
                lastOutBytes = currentOutBytes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing adapters: {ex.Message}");
            }
        }

        private string GetAdapterTypeName(NetworkInterface nic)
        {
            switch (nic.NetworkInterfaceType)
            {
                case NetworkInterfaceType.Wireless80211: return App.T("ConnType_WiFi", "Wi-Fi");
                case NetworkInterfaceType.Ethernet: return App.T("ConnType_Ethernet", "Ethernet");
                case NetworkInterfaceType.GigabitEthernet:
                case NetworkInterfaceType.FastEthernetT:
                case NetworkInterfaceType.FastEthernetFx:
                    return "Ethernet";
                case NetworkInterfaceType.Ppp:
                    return "PPP";
                case NetworkInterfaceType.Tunnel: return App.T("ConnType_VPN", "VPN");
                default:
                    var name = nic.Name.ToLower();
                    if (name.Contains("wi-fi") || name.Contains("wifi") || name.Contains("wireless"))
                        return "Wi-Fi";
                    if (name.Contains("ethernet") || name.Contains("lan"))
                        return "Ethernet";
                    return "Network";
            }
        }

        private void UpdateCurrentBytes()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();

                if (currentAdapter != "" && currentAdapter != "All")
                {
                    foreach (var nic in nics)
                    {
                        if (nic.Name == currentAdapter && nic.OperationalStatus == OperationalStatus.Up)
                        {
                            var stats = nic.GetIPv4Statistics();
                            currentInBytes = stats.BytesReceived;
                            currentOutBytes = stats.BytesSent;
                            currentAdapterType = GetAdapterTypeName(nic);
                            break;
                        }
                    }
                }
                else
                {
                    currentInBytes = 0;
                    currentOutBytes = 0;
                    foreach (var nic in nics)
                    {
                        if (nic.OperationalStatus == OperationalStatus.Up &&
                            nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        {
                            var stats = nic.GetIPv4Statistics();
                            currentInBytes += stats.BytesReceived;
                            currentOutBytes += stats.BytesSent;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating bytes: {ex.Message}");
            }
        }

        #endregion

        #region ================== TIMER CONTROL ==================

        public void Start()
        {
            int intervalMs = 1000;
            if (App.CurrentSettings != null && App.CurrentSettings.UpdateInterval > 0)
                intervalMs = App.CurrentSettings.UpdateInterval;

            intervalSeconds = intervalMs / 1000.0;

            timer = new Timer(intervalMs);
            timer.Elapsed += OnTimedEvent;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        private void StartHistoryAutoSave()
        {
            historySaveTimer = new Timer(60_000);
            historySaveTimer.Elapsed += (s, e) => FlushHistoryToDisk();
            historySaveTimer.AutoReset = true;
            historySaveTimer.Enabled = true;
        }

        private void FlushHistoryToDisk()
        {
            try
            {
                long deltaUp = todayUpload - lastSavedUpload;
                long deltaDown = todayDownload - lastSavedDownload;

                if (deltaUp > 0 || deltaDown > 0)
                {
                    historyService.AddDayData(currentDate, deltaUp, deltaDown);
                    lastSavedUpload = todayUpload;
                    lastSavedDownload = todayDownload;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FlushHistory error: {ex.Message}");
            }
        }

        public void Stop()
        {
            if (timer != null) { timer.Enabled = false; timer.Dispose(); timer = null; }
            if (historySaveTimer != null) { historySaveTimer.Enabled = false; historySaveTimer.Dispose(); historySaveTimer = null; }
        }

        #endregion

        #region ================== NETWORK STATS UPDATE ==================

        private void OnTimedEvent(object sender, ElapsedEventArgs e) => UpdateNetworkStats();

        private void UpdateNetworkStats()
        {
            try
            {
                UpdateCurrentBytes();

                if (DateTime.Today != currentDate)
                {
                    FlushHistoryToDisk();
                    currentDate = DateTime.Today;
                    todayUpload = 0;
                    todayDownload = 0;
                    lastSavedUpload = 0;
                    lastSavedDownload = 0;
                    lastInBytes = currentInBytes;
                    lastOutBytes = currentOutBytes;

                    _trafficAlertFired = false;
                    _alertFiredDate = DateTime.MinValue;

                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            App.ResetTrayIconWarning();
                        });
                    }
                }

                long inDelta = 0, outDelta = 0;
                bool connectionChanged = currentInBytes < lastInBytes || currentOutBytes < lastOutBytes;

                if (!connectionChanged && lastInBytes > 0 && lastOutBytes > 0)
                {
                    inDelta = currentInBytes - lastInBytes;
                    outDelta = currentOutBytes - lastOutBytes;
                }

                double inSpeed = inDelta / intervalSeconds;
                double outSpeed = outDelta / intervalSeconds;

                todayUpload += outDelta;
                todayDownload += inDelta;

                CheckTrafficLimit();

                if (inSpeed < 1 && outSpeed < 1)
                {
                    zeroSpeedCount++;
                    if (zeroSpeedCount >= 30 && App.CurrentSettings != null && App.CurrentSettings.AutoSelect)
                    {
                        AutoSelectConnection();
                        zeroSpeedCount = 0;
                    }
                }
                else zeroSpeedCount = 0;

                var data = new NetworkData
                {
                    UploadSpeed = outSpeed,
                    DownloadSpeed = inSpeed,
                    TodayUploadBytes = todayUpload,
                    TodayDownloadBytes = todayDownload,
                    ActiveConnection = currentAdapter,
                    ActiveConnectionType = currentAdapterType,
                    LastUpdate = DateTime.Now
                };

                DataUpdated?.Invoke(data);

                lastInBytes = currentInBytes;
                lastOutBytes = currentOutBytes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating stats: {ex.Message}");
            }
        }

        #endregion

        #region ================== TRAFFIC LIMIT ==================

        private void CheckTrafficLimit()
        {
            try
            {
                var settings = App.CurrentSettings;

                if (settings == null || !settings.TrafficTipEnable) return;
                if (_trafficAlertFired) return;

                long limitBytes = settings.TrafficTipUnit == 1
                    ? (long)settings.TrafficTipValue * 1024 * 1024 * 1024
                    : (long)settings.TrafficTipValue * 1024 * 1024;

                long totalToday = todayUpload + todayDownload;

                if (totalToday >= limitBytes)
                {
                    _trafficAlertFired = true;
                    _alertFiredDate = DateTime.Today;
                    TrafficLimitReached?.Invoke(totalToday, limitBytes);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CheckTrafficLimit error: {ex.Message}");
            }
        }

        #endregion

        #region ================== AUTO SELECT CONNECTION ==================

        private void AutoSelectConnection()
        {
            try
            {
                long maxBytes = 0;
                string bestAdapter = currentAdapter;
                NetworkInterface bestNic = null;

                var nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                    {
                        var stats = nic.GetIPv4Statistics();
                        long totalBytes = stats.BytesReceived + stats.BytesSent;

                        if (totalBytes > maxBytes)
                        {
                            maxBytes = totalBytes;
                            bestAdapter = nic.Name;
                            bestNic = nic;
                        }
                    }
                }

                if (bestAdapter != currentAdapter)
                {
                    currentAdapter = bestAdapter;
                    if (bestNic != null) currentAdapterType = GetAdapterTypeName(bestNic);
                    UpdateCurrentBytes();
                    lastInBytes = currentInBytes;
                    lastOutBytes = currentOutBytes;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error auto selecting: {ex.Message}");
            }
        }

        #endregion

        #region ================== CONNECTION MANAGEMENT ==================

        public List<ConnectionInfo> GetConnections()
        {
            var list = new List<ConnectionInfo>();
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var nic in nics)
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                    var ipProps = nic.GetIPProperties();
                    string ip = "0.0.0.0", subnet = "N/A", gateway = "N/A", dns = "N/A";

                    foreach (var ipAddr in ipProps.UnicastAddresses)
                    {
                        if (ipAddr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            ip = ipAddr.Address.ToString();
                            subnet = ipAddr.IPv4Mask?.ToString() ?? "N/A";
                            break;
                        }
                    }

                    var gw = ipProps.GatewayAddresses.FirstOrDefault();
                    if (gw != null) gateway = gw.Address.ToString();

                    var dnsServers = ipProps.DnsAddresses
                        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        .Select(a => a.ToString());
                    if (dnsServers.Any()) dns = string.Join(", ", dnsServers);

                    long bytesReceived = 0, bytesSent = 0;
                    try
                    {
                        var stats = nic.GetIPv4Statistics();
                        bytesReceived = stats.BytesReceived;
                        bytesSent = stats.BytesSent;
                    }
                    catch { }

                    list.Add(new ConnectionInfo
                    {
                        Name = nic.Name,
                        Description = nic.Description,
                        IpAddress = ip,
                        SubnetMask = subnet,
                        Gateway = gateway,
                        DnsServers = dns,
                        MacAddress = nic.GetPhysicalAddress().ToString(),
                        Speed = nic.Speed,
                        IsActive = nic.OperationalStatus == OperationalStatus.Up,
                        Type = GetAdapterTypeName(nic),
                        BytesReceived = bytesReceived,
                        BytesSent = bytesSent
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting connections: {ex.Message}");
            }
            return list;
        }

        public void SwitchConnection(string name)
        {
            currentAdapter = name;
            UpdateCurrentBytes();
            lastInBytes = currentInBytes;
            lastOutBytes = currentOutBytes;
            zeroSpeedCount = 0;
        }

        public void SelectAllConnections(bool selectAll)
        {
            if (selectAll)
            {
                currentAdapter = "All";
                UpdateCurrentBytes();
                lastInBytes = currentInBytes;
                lastOutBytes = currentOutBytes;
            }
        }

        #endregion

        #region ================== DISPOSE ==================

        public void Dispose()
        {
            FlushHistoryToDisk();
            Stop();
        }

        #endregion
    }

    #region ================== CONNECTION INFO CLASS ==================

    public class ConnectionInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string IpAddress { get; set; }
        public string SubnetMask { get; set; }
        public string Gateway { get; set; }
        public string DnsServers { get; set; }
        public string MacAddress { get; set; }
        public long Speed { get; set; }
        public bool IsActive { get; set; }
        public string Type { get; set; }
        public long BytesReceived { get; set; }
        public long BytesSent { get; set; }

        public string IPAddress => IpAddress ?? App.T("Common_NA", "N/A");
        public string MAC => FormatMac(MacAddress);
        public string SpeedText => FormatSpeed(Speed);
        public string BytesReceivedText => FormatBytes(BytesReceived);
        public string BytesSentText => FormatBytes(BytesSent);

        public string TypeIcon
        {
            get
            {
                switch (Type)
                {
                    case "Wi-Fi": return "\uE701";
                    case "Ethernet": return "\uE839";
                    case "VPN": return "\uE8AF";
                    default: return "\uE968";
                }
            }
        }

        private string FormatMac(string mac)
        {
            if (string.IsNullOrEmpty(mac) || mac.Length < 12) return App.T("Common_NA", "N/A");
            return string.Join(":", Enumerable.Range(0, 6)
                .Select(i => mac.Substring(i * 2, 2)));
        }

        private string FormatSpeed(long bps)
        {
            if (bps <= 0) return App.T("Common_NA", "N/A");
            if (bps >= 1_000_000_000) return $"{bps / 1_000_000_000.0:F0} Gbps";
            if (bps >= 1_000_000) return $"{bps / 1_000_000.0:F0} Mbps";
            return $"{bps / 1_000.0:F0} Kbps";
        }

        private string FormatBytes(long b)
        {
            if (b >= 1_073_741_824) return $"{b / 1_073_741_824.0:F2} GB";
            if (b >= 1_048_576) return $"{b / 1_048_576.0:F2} MB";
            if (b >= 1024) return $"{b / 1024.0:F2} KB";
            return $"{b} B";
        }
    }

    #endregion
}