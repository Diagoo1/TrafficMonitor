using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TrafficMonitor.Services
{
    public class GeoServerSelector
    {
        #region ================== PRIVATE FIELDS ==================

        private readonly HttpClient _client;
        private List<ServerInfo> _servers;
        private ServerInfo _cachedBestServer;
        private DateTime _cacheExpiry;

        #endregion

        #region ================== DEFAULT SERVERS ==================

        private readonly List<ServerInfo> _defaultServers = new List<ServerInfo>
        {
            // Africa & Middle East
            new ServerInfo { Name = "Cloudflare Cairo", Location = "Cairo", Country = "Egypt", CountryCode = "EG", Region = "Africa", BaseUrl = "https://speed.cloudflare.com", Latitude = 30.0444, Longitude = 31.2357, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Dubai", Location = "Dubai", Country = "UAE", CountryCode = "AE", Region = "Middle East", BaseUrl = "https://speed.cloudflare.com", Latitude = 25.2048, Longitude = 55.2708, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Johannesburg", Location = "Johannesburg", Country = "South Africa", CountryCode = "ZA", Region = "Africa", BaseUrl = "https://speed.cloudflare.com", Latitude = -26.2041, Longitude = 28.0473, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Lagos", Location = "Lagos", Country = "Nigeria", CountryCode = "NG", Region = "Africa", BaseUrl = "https://speed.cloudflare.com", Latitude = 6.5244, Longitude = 3.3792, Priority = 1 },
            
            // Europe
            new ServerInfo { Name = "Cloudflare London", Location = "London", Country = "UK", CountryCode = "GB", Region = "Europe", BaseUrl = "https://speed.cloudflare.com", Latitude = 51.5074, Longitude = -0.1278, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Frankfurt", Location = "Frankfurt", Country = "Germany", CountryCode = "DE", Region = "Europe", BaseUrl = "https://speed.cloudflare.com", Latitude = 50.1109, Longitude = 8.6821, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Paris", Location = "Paris", Country = "France", CountryCode = "FR", Region = "Europe", BaseUrl = "https://speed.cloudflare.com", Latitude = 48.8566, Longitude = 2.3522, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Amsterdam", Location = "Amsterdam", Country = "Netherlands", CountryCode = "NL", Region = "Europe", BaseUrl = "https://speed.cloudflare.com", Latitude = 52.3676, Longitude = 4.9041, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Stockholm", Location = "Stockholm", Country = "Sweden", CountryCode = "SE", Region = "Europe", BaseUrl = "https://speed.cloudflare.com", Latitude = 59.3293, Longitude = 18.0686, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Madrid", Location = "Madrid", Country = "Spain", CountryCode = "ES", Region = "Europe", BaseUrl = "https://speed.cloudflare.com", Latitude = 40.4168, Longitude = -3.7038, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Milan", Location = "Milan", Country = "Italy", CountryCode = "IT", Region = "Europe", BaseUrl = "https://speed.cloudflare.com", Latitude = 45.4642, Longitude = 9.1900, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Warsaw", Location = "Warsaw", Country = "Poland", CountryCode = "PL", Region = "Europe", BaseUrl = "https://speed.cloudflare.com", Latitude = 52.2297, Longitude = 21.0122, Priority = 1 },
            
            // North America
            new ServerInfo { Name = "Cloudflare New York", Location = "New York", Country = "USA", CountryCode = "US", Region = "North America", BaseUrl = "https://speed.cloudflare.com", Latitude = 40.7128, Longitude = -74.0060, Priority = 1 },
            new ServerInfo { Name = "Cloudflare San Francisco", Location = "San Francisco", Country = "USA", CountryCode = "US", Region = "North America", BaseUrl = "https://speed.cloudflare.com", Latitude = 37.7749, Longitude = -122.4194, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Chicago", Location = "Chicago", Country = "USA", CountryCode = "US", Region = "North America", BaseUrl = "https://speed.cloudflare.com", Latitude = 41.8781, Longitude = -87.6298, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Toronto", Location = "Toronto", Country = "Canada", CountryCode = "CA", Region = "North America", BaseUrl = "https://speed.cloudflare.com", Latitude = 43.6532, Longitude = -79.3832, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Mexico City", Location = "Mexico City", Country = "Mexico", CountryCode = "MX", Region = "North America", BaseUrl = "https://speed.cloudflare.com", Latitude = 19.4326, Longitude = -99.1332, Priority = 1 },
            
            // South America
            new ServerInfo { Name = "Cloudflare São Paulo", Location = "São Paulo", Country = "Brazil", CountryCode = "BR", Region = "South America", BaseUrl = "https://speed.cloudflare.com", Latitude = -23.5505, Longitude = -46.6333, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Buenos Aires", Location = "Buenos Aires", Country = "Argentina", CountryCode = "AR", Region = "South America", BaseUrl = "https://speed.cloudflare.com", Latitude = -34.6037, Longitude = -58.3816, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Santiago", Location = "Santiago", Country = "Chile", CountryCode = "CL", Region = "South America", BaseUrl = "https://speed.cloudflare.com", Latitude = -33.4489, Longitude = -70.6693, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Bogotá", Location = "Bogotá", Country = "Colombia", CountryCode = "CO", Region = "South America", BaseUrl = "https://speed.cloudflare.com", Latitude = 4.7110, Longitude = -74.0721, Priority = 1 },
            
            // Asia & Pacific
            new ServerInfo { Name = "Cloudflare Singapore", Location = "Singapore", Country = "Singapore", CountryCode = "SG", Region = "Asia", BaseUrl = "https://speed.cloudflare.com", Latitude = 1.3521, Longitude = 103.8198, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Tokyo", Location = "Tokyo", Country = "Japan", CountryCode = "JP", Region = "Asia", BaseUrl = "https://speed.cloudflare.com", Latitude = 35.6762, Longitude = 139.6503, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Seoul", Location = "Seoul", Country = "South Korea", CountryCode = "KR", Region = "Asia", BaseUrl = "https://speed.cloudflare.com", Latitude = 37.5665, Longitude = 126.9780, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Mumbai", Location = "Mumbai", Country = "India", CountryCode = "IN", Region = "Asia", BaseUrl = "https://speed.cloudflare.com", Latitude = 19.0760, Longitude = 72.8777, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Sydney", Location = "Sydney", Country = "Australia", CountryCode = "AU", Region = "Oceania", BaseUrl = "https://speed.cloudflare.com", Latitude = -33.8688, Longitude = 151.2093, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Hong Kong", Location = "Hong Kong", Country = "Hong Kong", CountryCode = "HK", Region = "Asia", BaseUrl = "https://speed.cloudflare.com", Latitude = 22.3193, Longitude = 114.1694, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Taipei", Location = "Taipei", Country = "Taiwan", CountryCode = "TW", Region = "Asia", BaseUrl = "https://speed.cloudflare.com", Latitude = 25.0330, Longitude = 121.5654, Priority = 1 },
            new ServerInfo { Name = "Cloudflare Kuala Lumpur", Location = "Kuala Lumpur", Country = "Malaysia", CountryCode = "MY", Region = "Asia", BaseUrl = "https://speed.cloudflare.com", Latitude = 3.1390, Longitude = 101.6869, Priority = 1 },
            
            // CDN Fallbacks
            new ServerInfo { Name = "OVH France", Location = "Paris", Country = "France", CountryCode = "FR", Region = "Europe", BaseUrl = "https://proof.ovh.net/files", Priority = 2 },
            new ServerInfo { Name = "Hetzner Germany", Location = "Nuremberg", Country = "Germany", CountryCode = "DE", Region = "Europe", BaseUrl = "https://speed.hetzner.de", Priority = 2 },
            new ServerInfo { Name = "Linode London", Location = "London", Country = "UK", CountryCode = "GB", Region = "Europe", BaseUrl = "https://speedtest.london.linode.com", Priority = 2 }
        };

        #endregion

        #region ================== CONSTRUCTOR ==================

        public GeoServerSelector()
        {
            _client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _servers = new List<ServerInfo>(_defaultServers);
        }

        #endregion

        #region ================== PUBLIC METHODS ==================

        public async Task<ServerInfo> SelectBestServerAsync(CancellationToken ct)
        {
            if (_cachedBestServer != null && DateTime.Now < _cacheExpiry)
                return _cachedBestServer;

            try
            {
                var userLocation = await GetUserLocationAsync(ct);

                foreach (var server in _servers)
                {
                    if (userLocation != null)
                    {
                        server.AvgLatencyMs = CalculateDistance(
                            userLocation.Latitude, userLocation.Longitude,
                            server.Latitude, server.Longitude) * 0.1;
                    }

                    if (server.Priority == 1)
                    {
                        var ping = await PingServerAsync(server, ct);
                        if (ping > 0)
                            server.AvgLatencyMs = ping;
                    }
                }

                var bestServer = _servers
                    .Where(s => s.IsActive || s.Priority == 1)
                    .OrderBy(s => s.AvgLatencyMs > 0 ? s.AvgLatencyMs : double.MaxValue)
                    .ThenBy(s => s.Priority)
                    .FirstOrDefault();

                if (bestServer != null)
                {
                    bestServer.IsActive = true;
                    _cachedBestServer = bestServer;
                    _cacheExpiry = DateTime.Now.AddMinutes(5);
                }

                return bestServer ?? _servers.First();
            }
            catch
            {
                return _servers.First(s => s.Priority == 1);
            }
        }

        #endregion

        #region ================== PRIVATE METHODS ==================

        private async Task<ServerInfo> GetUserLocationAsync(CancellationToken ct)
        {
            try
            {
                var response = await _client.GetStringAsync("https://ipapi.co/json/");
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;

                var lat = root.TryGetProperty("latitude", out var latProp) ? latProp.GetDouble() : 0;
                var lon = root.TryGetProperty("longitude", out var lonProp) ? lonProp.GetDouble() : 0;
                var countryCode = root.TryGetProperty("country_code", out var ccProp) ? ccProp.GetString() : "";
                var city = root.TryGetProperty("city", out var cityProp) ? cityProp.GetString() : "";

                if (lat != 0 && lon != 0)
                {
                    return new ServerInfo
                    {
                        Location = city ?? "Unknown",
                        CountryCode = countryCode ?? "",
                        Latitude = lat,
                        Longitude = lon
                    };
                }
            }
            catch { }
            return null;
        }

        private async Task<double> PingServerAsync(ServerInfo server, CancellationToken ct)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var req = new HttpRequestMessage(HttpMethod.Head, $"{server.BaseUrl}/__down?bytes=100");
                var resp = await _client.SendAsync(req, ct);
                sw.Stop();

                if (resp.IsSuccessStatusCode)
                    return sw.ElapsedMilliseconds;
            }
            catch { }
            return -1;
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180;

        #endregion
    }
}