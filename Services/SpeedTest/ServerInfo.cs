using System;

namespace TrafficMonitor.Services
{
    public class ServerInfo
    {
        #region ================== PROPERTIES ==================

        public string Name { get; set; }
        public string Location { get; set; }
        public string Country { get; set; }
        public string CountryCode { get; set; }
        public string Region { get; set; }
        public string BaseUrl { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsActive { get; set; }
        public int Priority { get; set; }
        public double AvgLatencyMs { get; set; }

        #endregion

        #region ================== METHODS ==================

        public override string ToString() => $"{Name} ({Location})";

        #endregion
    }
}