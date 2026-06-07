using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;

namespace TrafficMonitor.Services
{
    #region ================== PROCESS NETWORK INFO CLASS ==================

    public class ProcessNetworkInfo
    {
        public int PID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double UploadSpeed { get; set; }
        public double DownloadSpeed { get; set; }
        public long TotalUpload { get; set; }
        public long TotalDownload { get; set; }
        public Icon ProcessIcon { get; set; }

        public string UploadText => FormatSpeed(UploadSpeed);
        public string DownloadText => FormatSpeed(DownloadSpeed);
        public string TotalText => FormatBytes(TotalUpload + TotalDownload);
        public long TotalBytes => TotalUpload + TotalDownload;

        private string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec >= 1024 * 1024)
                return $"{bytesPerSec / (1024 * 1024):F2} MB/s";
            if (bytesPerSec >= 1024)
                return $"{bytesPerSec / 1024:F2} KB/s";
            return $"{bytesPerSec:F0} B/s";
        }

        private string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            if (bytes >= 1024 * 1024)
                return $"{bytes / (1024.0 * 1024):F2} MB";
            if (bytes >= 1024)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} B";
        }
    }

    #endregion

    public class ProcessNetworkService : IDisposable
    {
        #region ================== WIN32 API ==================

        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int dwSize,
            bool sort, int ipVersion, TCP_TABLE_CLASS tblClass, uint reserved);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern uint GetExtendedUdpTable(IntPtr pUdpTable, ref int dwSize,
            bool sort, int ipVersion, UDP_TABLE_CLASS tblClass, uint reserved);

        [DllImport("kernel32.dll")]
        static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS counters);

        enum TCP_TABLE_CLASS { TCP_TABLE_OWNER_PID_ALL = 5 }
        enum UDP_TABLE_CLASS { UDP_TABLE_OWNER_PID = 1 }

        [StructLayout(LayoutKind.Sequential)]
        struct MIB_TCPROW_OWNER_PID
        {
            public uint state, localAddr, localPort, remoteAddr, remotePort, owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MIB_UDPROW_OWNER_PID
        {
            public uint localAddr, localPort, owningPid;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct IO_COUNTERS
        {
            public ulong ReadOperationCount, WriteOperationCount,
                         OtherOperationCount, ReadTransferCount,
                         WriteTransferCount, OtherTransferCount;
        }

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private Timer _timer;
        private readonly object _lock = new();
        private Dictionary<int, (long read, long write)> _lastIO = new();
        private List<ProcessNetworkInfo> _currentData = new();
        private double _intervalSec = 1.0;
        private static Dictionary<int, Icon> _iconCache = new();

        #endregion

        #region ================== EVENTS ==================

        public event Action<List<ProcessNetworkInfo>> DataUpdated;

        #endregion

        #region ================== CONSTRUCTOR & LIFECYCLE ==================

        public ProcessNetworkService() => Start();

        public void Start()
        {
            _timer = new Timer(1000);
            _timer.Elapsed += (s, e) => Update();
            _timer.AutoReset = true;
            _timer.Start();
        }

        public void Stop()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        }

        public void Dispose() => Stop();

        #endregion

        #region ================== CORE UPDATE ==================

        private void Update()
        {
            try
            {
                var activePids = GetActivePids();
                var result = new List<ProcessNetworkInfo>();

                foreach (int pid in activePids)
                {
                    try
                    {
                        var proc = Process.GetProcessById(pid);
                        if (proc == null) continue;

                        GetProcessIoCounters(proc.Handle, out IO_COUNTERS io);

                        long readNow = (long)io.ReadTransferCount;
                        long writeNow = (long)io.WriteTransferCount;

                        double downSpeed = 0, upSpeed = 0;

                        if (_lastIO.TryGetValue(pid, out var last))
                        {
                            long readDelta = Math.Max(0, readNow - last.read);
                            long writeDelta = Math.Max(0, writeNow - last.write);
                            downSpeed = readDelta / _intervalSec;
                            upSpeed = writeDelta / _intervalSec;
                        }

                        _lastIO[pid] = (readNow, writeNow);

                        string procName = GetSafeName(proc);
                        Icon icon = GetProcessIcon(proc);

                        result.Add(new ProcessNetworkInfo
                        {
                            PID = pid,
                            Name = procName,
                            Description = GetDescription(proc),
                            UploadSpeed = upSpeed,
                            DownloadSpeed = downSpeed,
                            TotalUpload = writeNow,
                            TotalDownload = readNow,
                            ProcessIcon = icon
                        });

                        proc.Dispose();
                    }
                    catch { }
                }

                var sorted = result.OrderByDescending(p => p.UploadSpeed + p.DownloadSpeed).ToList();

                lock (_lock) { _currentData = sorted; }
                DataUpdated?.Invoke(sorted);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ProcessNetworkService error: {ex.Message}");
            }
        }

        #endregion

        #region ================== GET ACTIVE PIDS ==================

        private HashSet<int> GetActivePids()
        {
            var pids = new HashSet<int>();

            //--------------- TCP ---------------
            int tcpSize = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref tcpSize, false, 2,
                TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0);

            IntPtr tcpPtr = Marshal.AllocHGlobal(tcpSize);
            try
            {
                if (GetExtendedTcpTable(tcpPtr, ref tcpSize, false, 2,
                    TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_ALL, 0) == 0)
                {
                    int rowCount = Marshal.ReadInt32(tcpPtr);
                    IntPtr ptr = tcpPtr + 4;
                    int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

                    for (int i = 0; i < rowCount; i++)
                    {
                        var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(ptr);
                        if (row.owningPid > 4)
                            pids.Add((int)row.owningPid);
                        ptr += rowSize;
                    }
                }
            }
            finally { Marshal.FreeHGlobal(tcpPtr); }

            //--------------- UDP ---------------
            int udpSize = 0;
            GetExtendedUdpTable(IntPtr.Zero, ref udpSize, false, 2,
                UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0);

            IntPtr udpPtr = Marshal.AllocHGlobal(udpSize);
            try
            {
                if (GetExtendedUdpTable(udpPtr, ref udpSize, false, 2,
                    UDP_TABLE_CLASS.UDP_TABLE_OWNER_PID, 0) == 0)
                {
                    int rowCount = Marshal.ReadInt32(udpPtr);
                    IntPtr ptr = udpPtr + 4;
                    int rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();

                    for (int i = 0; i < rowCount; i++)
                    {
                        var row = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(ptr);
                        if (row.owningPid > 4)
                            pids.Add((int)row.owningPid);
                        ptr += rowSize;
                    }
                }
            }
            finally { Marshal.FreeHGlobal(udpPtr); }

            return pids;
        }

        #endregion

        #region ================== HELPERS ==================

        private string GetSafeName(Process p)
        {
            try { return p.ProcessName; }
            catch { return "Unknown"; }
        }

        private string GetDescription(Process p)
        {
            try
            {
                var path = p.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    var info = FileVersionInfo.GetVersionInfo(path);
                    return string.IsNullOrEmpty(info.FileDescription)
                        ? p.ProcessName : info.FileDescription;
                }
            }
            catch { }
            return p.ProcessName;
        }

        private Icon GetProcessIcon(Process p)
        {
            try
            {
                if (_iconCache.TryGetValue(p.Id, out var cached))
                    return cached;

                var path = p.MainModule?.FileName;
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var icon = Icon.ExtractAssociatedIcon(path);
                    if (icon != null)
                    {
                        _iconCache[p.Id] = icon;
                        return icon;
                    }
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        public List<ProcessNetworkInfo> GetCurrentData()
        {
            lock (_lock) { return _currentData.ToList(); }
        }

        #endregion
    }
}