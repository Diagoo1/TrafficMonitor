using System;
using System.Threading;
using System.Threading.Tasks;

namespace TrafficMonitor.Services
{
    public interface ISpeedTestProvider
    {
        #region ================== PROPERTIES ==================

        string Name { get; }
        int Priority { get; }

        #endregion

        #region ================== METHODS ==================

        Task<bool> IsAvailableAsync(CancellationToken ct);
        Task<double> TestDownloadAsync(CancellationToken ct, Action<double, double> onProgress);
        Task<double> TestUploadAsync(CancellationToken ct, Action<double, double> onProgress);
        Task<(double ping, double jitter)> TestPingAsync(CancellationToken ct);
        Task<double> TestPacketLossAsync(CancellationToken ct);

        #endregion
    }
}