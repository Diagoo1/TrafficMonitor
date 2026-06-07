using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using TrafficMonitor.Models;

namespace TrafficMonitor.Services
{
    public class HistoryService
    {
        #region ================== CONSTANTS ==================

        private const int MIN_SAVE_INTERVAL_SECONDS = 30;

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private readonly string _historyPath;
        private List<HistoryData> _history;
        private readonly object _lock = new object();
        private DateTime _lastSaveTime = DateTime.MinValue;

        #endregion

        #region ================== CONSTRUCTOR ==================

        public HistoryService()
        {
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "TrafficMonitor");

            if (!Directory.Exists(appDataFolder))
                Directory.CreateDirectory(appDataFolder);

            _historyPath = Path.Combine(appDataFolder, "history.json");
            _history = LoadHistory();
        }

        #endregion

        #region ================== LOAD & SAVE ==================

        private List<HistoryData> LoadHistory()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_historyPath))
                    {
                        var json = File.ReadAllText(_historyPath);
                        var history = JsonSerializer.Deserialize<List<HistoryData>>(json);
                        if (history != null) return history;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading history: {ex.Message}");
                }
                return new List<HistoryData>();
            }
        }

        private void SaveHistory_NoLock()
        {
            try
            {
                var json = JsonSerializer.Serialize(_history,
                    new JsonSerializerOptions { WriteIndented = true });
                var tempPath = _historyPath + ".tmp";

                File.WriteAllText(tempPath, json);

                if (File.Exists(_historyPath))
                    File.Delete(_historyPath);
                File.Move(tempPath, _historyPath);

                _lastSaveTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving history: {ex.Message}");
            }
        }

        private bool ShouldSaveNow()
        {
            return (DateTime.Now - _lastSaveTime).TotalSeconds >= MIN_SAVE_INTERVAL_SECONDS;
        }

        public void SaveImmediate()
        {
            lock (_lock) { SaveHistory_NoLock(); }
        }

        public DateTime GetLastSaveTime()
        {
            lock (_lock) { return _lastSaveTime; }
        }

        #endregion

        #region ================== DATA OPERATIONS ==================

        public void AddDayData(DateTime date, long uploadBytes, long downloadBytes)
        {
            lock (_lock)
            {
                var existing = _history.FirstOrDefault(h => h.Date.Date == date.Date);

                if (existing != null)
                {
                    existing.UploadBytes += uploadBytes;
                    existing.DownloadBytes += downloadBytes;
                }
                else
                {
                    _history.Add(new HistoryData
                    {
                        Date = date.Date,
                        UploadBytes = uploadBytes,
                        DownloadBytes = downloadBytes
                    });
                }

                if (ShouldSaveNow())
                    SaveHistory_NoLock();
            }
        }

        public void SetDayData(DateTime date, long uploadBytes, long downloadBytes)
        {
            lock (_lock)
            {
                var existing = _history.FirstOrDefault(h => h.Date.Date == date.Date);

                if (existing != null)
                {
                    existing.UploadBytes = uploadBytes;
                    existing.DownloadBytes = downloadBytes;
                }
                else
                {
                    _history.Add(new HistoryData
                    {
                        Date = date.Date,
                        UploadBytes = uploadBytes,
                        DownloadBytes = downloadBytes
                    });
                }

                SaveHistory_NoLock();
            }
        }

        #endregion

        #region ================== QUERIES ==================

        public List<HistoryData> GetHistory()
        {
            lock (_lock)
            {
                return _history.OrderByDescending(h => h.Date).ToList();
            }
        }

        public List<HistoryData> GetHistoryFilled(DateTime fromDate)
        {
            lock (_lock)
            {
                var map = _history.ToDictionary(h => h.Date.Date, h => h);
                var result = new List<HistoryData>();

                for (var d = fromDate.Date; d <= DateTime.Today; d = d.AddDays(1))
                {
                    if (map.TryGetValue(d, out var item))
                    {
                        result.Add(new HistoryData
                        {
                            Date = d,
                            UploadBytes = item.UploadBytes,
                            DownloadBytes = item.DownloadBytes
                        });
                    }
                    else
                    {
                        result.Add(new HistoryData
                        {
                            Date = d,
                            UploadBytes = 0,
                            DownloadBytes = 0
                        });
                    }
                }
                return result.OrderByDescending(h => h.Date).ToList();
            }
        }

        #endregion
    }
}