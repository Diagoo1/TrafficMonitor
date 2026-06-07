using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TrafficMonitor.Services
{
    public class SpeedTestManager
    {
        #region ================== PRIVATE FIELDS ==================

        private readonly List<ISpeedTestProvider> _providers;

        #endregion

        #region ================== CONSTRUCTOR ==================

        public SpeedTestManager()
        {
            _providers = new List<ISpeedTestProvider>
            {
                new CloudflareProvider(),
                new MultiCdnProvider(),
                new LocalFallbackProvider()
            };
        }

        #endregion

        #region ================== PUBLIC METHODS ==================

        public async Task<ISpeedTestProvider> SelectBestProviderAsync(CancellationToken ct)
        {
            foreach (var provider in _providers.OrderBy(p => p.Priority))
            {
                try
                {
                    Debug.WriteLine($"🔍 Checking provider: {provider.Name}");
                    if (await provider.IsAvailableAsync(ct))
                    {
                        Debug.WriteLine($"✅ Selected provider: {provider.Name}");
                        return provider;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ Provider {provider.Name} failed: {ex.Message}");
                }
            }
            throw new Exception("No speed test provider available");
        }

        #endregion
    }
}