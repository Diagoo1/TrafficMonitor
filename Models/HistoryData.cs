using System;

namespace TrafficMonitor.Models
{
    public class HistoryData
    {
        #region ================== PROPERTIES ==================

        public DateTime Date { get; set; }
        public long UploadBytes { get; set; }
        public long DownloadBytes { get; set; }
        public long TotalBytes => UploadBytes + DownloadBytes;

        #endregion

        #region ================== FORMATTED PROPERTIES ==================

        public string FormattedDate => Date.ToString("yyyy/MM/dd");
        public string FormattedUpload => FormatBytes(UploadBytes);
        public string FormattedDownload => FormatBytes(DownloadBytes);
        public string FormattedTotal => FormatBytes(TotalBytes);

        #endregion

        #region ================== PRIVATE FORMATTING METHOD ==================

        private string FormatBytes(long bytes)
        {
            const double KB = 1024;
            const double MB = KB * 1024;
            const double GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / KB:F2} KB";
            return $"{bytes} B";
        }

        #endregion
    }
}