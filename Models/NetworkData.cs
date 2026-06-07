using System;

namespace TrafficMonitor.Models
{
    public class NetworkData
    {
        #region ================== PROPERTIES ==================

        public double UploadSpeed { get; set; }
        public double DownloadSpeed { get; set; }
        public long TodayUploadBytes { get; set; }
        public long TodayDownloadBytes { get; set; }
        public string ActiveConnection { get; set; } = "";
        public string ActiveConnectionType { get; set; } = "Network";
        public DateTime LastUpdate { get; set; }

        #endregion

        #region ================== TEXT FORMATTING PROPERTIES ==================

        public string UploadText => FormatSpeed(UploadSpeed);
        public string DownloadText => FormatSpeed(DownloadSpeed);
        public string TodayText => FormatTraffic(TodayUploadBytes + TodayDownloadBytes);

        #endregion

        #region ================== CONNECTION TYPE ICON ==================

        public string ConnectionTypeIcon
        {
            get
            {
                switch (ActiveConnectionType)
                {
                    case "Wi-Fi": return "\uE701";
                    case "Ethernet": return "\uE839";
                    case "VPN": return "\uE8AF";
                    default: return "\uE968";
                }
            }
        }

        #endregion

        #region ================== PRIVATE FORMATTING METHODS ==================

        private string FormatSpeed(double speed)
        {
            const double KB = 1024;
            const double MB = KB * 1024;
            if (speed >= MB) return $"{speed / MB:F2} MB/s";
            if (speed >= KB) return $"{speed / KB:F2} KB/s";
            return $"{speed:F0} B/s";
        }

        private string FormatTraffic(long bytes)
        {
            const double MB = 1024 * 1024;
            const double GB = MB * 1024;
            if (bytes >= GB) return $"{bytes / GB:F2} GB";
            if (bytes >= MB) return $"{bytes / MB:F2} MB";
            return $"{bytes / 1024:F2} KB";
        }

        #endregion
    }
}