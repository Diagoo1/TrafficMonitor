using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TrafficMonitor.Services;
using TrafficMonitor.Themes;

namespace TrafficMonitor.Views
{
    #region ================== VIEW MODEL ==================

    public class ProcessNetworkRow
    {
        public int PID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string UploadText { get; set; }
        public string DownloadText { get; set; }
        public string TotalText { get; set; }
        public double UploadBarWidth { get; set; }
        public double DownloadBarWidth { get; set; }
        public ImageSource IconSource { get; set; }
    }

    #endregion

    public partial class ProcessNetworkWindow : Window
    {
        #region ================== PRIVATE FIELDS ==================

        private ProcessNetworkService _service;
        private string _sortMode = "speed";

        #endregion

        #region ================== CONSTRUCTOR & INIT ==================

        public ProcessNetworkWindow()
        {
            InitializeComponent();

            ThemeManager.ApplyThemeToWindow(this);
            ThemeManager.ThemeChanged += OnThemeChanged;
            this.Closed += (s, e) =>
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                _service?.Dispose();
            };

            _service = new ProcessNetworkService();
            _service.DataUpdated += OnDataUpdated;
        }

        #endregion

        #region ================== THEME HANDLING ==================

        private void OnThemeChanged(bool isDark)
        {
            Dispatcher.Invoke(() => ThemeManager.ApplyThemeToWindow(this));
        }

        #endregion

        #region ================== DATA UPDATE HANDLER ==================

        private void OnDataUpdated(List<ProcessNetworkInfo> data)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    double maxUp = data.Count > 0 ? data.Max(p => p.UploadSpeed) : 1;
                    double maxDown = data.Count > 0 ? data.Max(p => p.DownloadSpeed) : 1;
                    if (maxUp < 1) maxUp = 1;
                    if (maxDown < 1) maxDown = 1;

                    IEnumerable<ProcessNetworkInfo> sorted = _sortMode switch
                    {
                        "total" => data.OrderByDescending(p => p.TotalBytes),
                        "name" => data.OrderBy(p => p.Name),
                        _ => data.OrderByDescending(p => p.UploadSpeed + p.DownloadSpeed)
                    };

                    var rows = sorted.Select(p => new ProcessNetworkRow
                    {
                        PID = p.PID,
                        Name = p.Name,
                        Description = p.Description,
                        UploadText = p.UploadText,
                        DownloadText = p.DownloadText,
                        TotalText = p.TotalText,
                        UploadBarWidth = p.UploadSpeed / maxUp * 80,
                        DownloadBarWidth = p.DownloadSpeed / maxDown * 80,
                        IconSource = ConvertIcon(p.ProcessIcon)
                    }).ToList();

                    ProcessList.ItemsSource = rows;

                    int activeCount = data.Count(p => p.UploadSpeed > 0 || p.DownloadSpeed > 0);
                    ProcessCountText.Text = App.T("ProcessNetwork_StatusFormat", "{0} processes ({1} active)",
                        data.Count, activeCount);
                    SubtitleText.Text = App.T("ProcessNetwork_LastUpdated", "Last updated: {0}",
                        DateTime.Now.ToString("HH:mm:ss"));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ProcessNetworkWindow update error: {ex.Message}");
                }
            });
        }

        #endregion

        #region ================== ICON CONVERSION ==================

        private static ImageSource ConvertIcon(Icon icon)
        {
            try
            {
                if (icon == null) return null;
                return Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
            }
            catch { return null; }
        }

        #endregion

        #region ================== SORT HANDLERS ==================

        private void SortSpeed_Click(object sender, MouseButtonEventArgs e)
            => SetSort("speed");

        private void SortTotal_Click(object sender, MouseButtonEventArgs e)
            => SetSort("total");

        private void SortName_Click(object sender, MouseButtonEventArgs e)
            => SetSort("name");

        private void SetSort(string mode)
        {
            _sortMode = mode;

            var accent = TryFindResource("DynamicAccent") as System.Windows.Media.Brush;
            var hover = TryFindResource("DynamicHoverBg") as System.Windows.Media.Brush;
            var white = System.Windows.Media.Brushes.White;
            var sub = TryFindResource("DynamicSubText") as System.Windows.Media.Brush;

            SortSpeedBtn.Background = mode == "speed" ? accent : hover;
            SortTotalBtn.Background = mode == "total" ? accent : hover;
            SortNameBtn.Background = mode == "name" ? accent : hover;

            if (SortSpeedBtn.Child is System.Windows.Controls.TextBlock t1)
                t1.Foreground = mode == "speed" ? white : sub;
            if (SortTotalBtn.Child is System.Windows.Controls.TextBlock t2)
                t2.Foreground = mode == "total" ? white : sub;
            if (SortNameBtn.Child is System.Windows.Controls.TextBlock t3)
                t3.Foreground = mode == "name" ? white : sub;
        }

        #endregion

        #region ================== WINDOW EVENTS ==================

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        #endregion
    }
}