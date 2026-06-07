using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using TrafficMonitor.Models;
using TrafficMonitor.Services;
using Color = System.Windows.Media.Color;
using Brushes = System.Windows.Media.Brushes;
using Brush = System.Windows.Media.Brush;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace TrafficMonitor.Views
{
    public partial class HistoryWindow : Window
    {
        #region ================== PRIVATE FIELDS ==================

        private HistoryService historyService;
        private List<HistoryData> allHistory;
        private int currentYear;
        private int currentMonth;
        private bool logarithmic = true;
        private System.Windows.Threading.DispatcherTimer _refreshTimer;
        private DateTime _installDate;

        #endregion

        #region ================== COLORS ==================

        private static readonly Color WeekendColor = Color.FromRgb(239, 68, 68);
        private static readonly Color WeekdayColor = Color.FromRgb(14, 165, 233);
        private static readonly Color TodayBorderColor = Color.FromRgb(14, 165, 233);

        #endregion

        #region ================== CONSTRUCTOR ==================

        public HistoryWindow()
        {
            InitializeComponent();
            historyService = App.NetworkService.GetHistoryService();

            _installDate = GetWindowsInstallDate();

            var rawHistory = historyService.GetHistory();
            if (rawHistory.Count > 0)
            {
                var oldestData = rawHistory.Min(h => h.Date);
                if (oldestData < _installDate)
                    _installDate = oldestData.Date;
            }

            allHistory = historyService.GetHistoryFilled(_installDate);

            currentYear = DateTime.Today.Year;
            currentMonth = DateTime.Today.Month;

            int startYear = _installDate.Year;
            YearCombo.Items.Clear();
            for (int y = startYear; y <= currentYear; y++)
                YearCombo.Items.Add(y);
            YearCombo.SelectedItem = currentYear;

            RefreshMonthCombo(currentYear);

            YearCombo.SelectionChanged += YearCombo_SelectionChanged;

            RefreshList();
            RefreshCalendar();

            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _refreshTimer.Tick += (s, e) =>
            {
                allHistory = historyService.GetHistoryFilled(_installDate);
                RefreshList();
                if (CalendarViewPanel.Visibility == Visibility.Visible)
                    RefreshCalendar();
            };
            _refreshTimer.Start();

            this.Closed += (s, e) => _refreshTimer?.Stop();
        }

        #endregion

        #region ================== INSTALL DATE ==================

        private DateTime GetWindowsInstallDate()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");

                if (key != null)
                {
                    var val = key.GetValue("InstallDate");
                    if (val != null)
                    {
                        long seconds = Convert.ToInt64(val);
                        var date = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime.Date;

                        if (date.Year >= 2009 && date.Year <= DateTime.Today.Year)
                            return date;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting install date: {ex.Message}");
            }

            return DateTime.Today.AddYears(-1);
        }

        #endregion

        #region ================== LIST VIEW ==================

        private void RefreshList()
        {
            if (HistoryList == null || allHistory == null) return;

            var items = new List<HistoryRow>();
            long maxTotal = allHistory.Count > 0 ? allHistory.Max(h => h.TotalBytes) : 1;
            if (maxTotal < 1) maxTotal = 1;

            double logMax = Math.Log10(maxTotal + 1);
            if (logMax <= 0) logMax = 1;

            foreach (var h in allHistory.OrderByDescending(x => x.Date))
            {
                double ratio;
                if (logarithmic)
                    ratio = Math.Log10(h.TotalBytes + 1) / logMax;
                else
                    ratio = (double)h.TotalBytes / maxTotal;

                if (double.IsNaN(ratio) || double.IsInfinity(ratio)) ratio = 0;
                ratio = Math.Max(0, Math.Min(1, ratio));

                items.Add(new HistoryRow
                {
                    Date = h.Date,
                    Upload = h.UploadBytes,
                    Download = h.DownloadBytes,
                    BarWidth = Math.Max(2, ratio * 140),
                    BarBrush = new SolidColorBrush(GetTrafficColor(h.TotalBytes))
                });
            }

            HistoryList.ItemsSource = items;
        }

        private Color GetTrafficColor(long bytes)
        {
            const long GB = 1024L * 1024 * 1024;
            const long TB = GB * 1024;

            if (bytes >= TB) return Color.FromRgb(0xAA, 0x22, 0x22);
            if (bytes >= 100 * GB) return Color.FromRgb(0xE5, 0x78, 0x45);
            if (bytes >= 10 * GB) return Color.FromRgb(0xE5, 0xC0, 0x3A);
            if (bytes >= GB) return Color.FromRgb(0x6B, 0xBE, 0x5C);
            return Color.FromRgb(0x45, 0xA9, 0xE5);
        }

        #endregion

        #region ================== CALENDAR HELPERS ==================

        private void RefreshMonthCombo(int year)
        {
            int prevSelected = currentMonth;

            MonthCombo.SelectionChanged -= CalendarRefresh;
            MonthCombo.Items.Clear();

            int maxMonth = (year == DateTime.Today.Year) ? DateTime.Today.Month : 12;
            int minMonth = (year == _installDate.Year) ? _installDate.Month : 1;

            for (int m = minMonth; m <= maxMonth; m++)
            {
                string monthName = new DateTime(year, m, 1).ToString("MMMM", CultureInfo.CurrentCulture);
                string displayText = $"{m} - {monthName}";

                MonthCombo.Items.Add(new ComboBoxItem
                {
                    Content = displayText,
                    Tag = m
                });
            }

            foreach (ComboBoxItem item in MonthCombo.Items)
            {
                if ((int)item.Tag == prevSelected)
                {
                    MonthCombo.SelectedItem = item;
                    break;
                }
            }

            if (MonthCombo.SelectedItem == null && MonthCombo.Items.Count > 0)
                MonthCombo.SelectedIndex = MonthCombo.Items.Count - 1;

            MonthCombo.SelectionChanged += CalendarRefresh;
        }

        private void YearCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (YearCombo.SelectedItem == null) return;
            currentYear = (int)YearCombo.SelectedItem;

            RefreshMonthCombo(currentYear);
            RefreshCalendar();
        }

        #endregion

        #region ================== CALENDAR VIEW ==================

        private void RefreshCalendar()
        {
            if (YearCombo.SelectedItem == null || MonthCombo.SelectedItem == null) return;

            currentYear = (int)YearCombo.SelectedItem;

            if (MonthCombo.SelectedItem is ComboBoxItem monthItem && monthItem.Tag != null)
                currentMonth = (int)monthItem.Tag;

            CalendarGrid.Children.Clear();
            UpdateCalendarFlowDirection();

            string[] daysOfWeek = {
                App.T("DaySun","Sun"), App.T("DayMon","Mon"), App.T("DayTue","Tue"),
                App.T("DayWed","Wed"), App.T("DayThu","Thu"), App.T("DayFri","Fri"),
                App.T("DaySat","Sat")
            };

            for (int i = 0; i < 7; i++)
            {
                bool isWeekend = (i == 0 || i == 6);

                Border headerBorder = new Border
                {
                    CornerRadius = new CornerRadius(6, 6, 2, 2),
                    Margin = new Thickness(1),
                    Background = isWeekend ? new SolidColorBrush(WeekendColor) : new SolidColorBrush(WeekdayColor),
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                TextBlock headerText = new TextBlock
                {
                    Text = daysOfWeek[i],
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12,
                    Foreground = Brushes.White
                };

                headerBorder.Child = headerText;
                Grid.SetRow(headerBorder, 0);
                Grid.SetColumn(headerBorder, i);
                CalendarGrid.Children.Add(headerBorder);
            }

            var firstDay = new DateTime(currentYear, currentMonth, 1);
            int startCol = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);

            var monthData = historyService.GetHistory()
                .Where(h => h.Date.Year == currentYear && h.Date.Month == currentMonth)
                .ToDictionary(h => h.Date.Day, h => h);

            long monthTotal = 0, monthUp = 0, monthDown = 0;

            int row = 1;
            int col = startCol;

            for (int day = 1; day <= daysInMonth; day++)
            {
                bool isToday = (day == DateTime.Today.Day && currentMonth == DateTime.Today.Month && currentYear == DateTime.Today.Year);
                bool isWeekend = (col == 0 || col == 6);
                bool isAlternate = (row + col) % 2 == 0;
                byte alpha = (byte)(isAlternate ? 25 : 12);

                Brush defaultBg = isWeekend
                    ? new SolidColorBrush(Color.FromArgb(alpha, WeekendColor.R, WeekendColor.G, WeekendColor.B))
                    : new SolidColorBrush(Color.FromArgb(alpha, WeekdayColor.R, WeekdayColor.G, WeekdayColor.B));

                Border cellBorder = new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(1),
                    Background = defaultBg,
                    Cursor = Cursors.Hand
                };

                if (isToday)
                {
                    cellBorder.BorderBrush = new SolidColorBrush(TodayBorderColor);
                    cellBorder.BorderThickness = new Thickness(1.5);
                }

                cellBorder.MouseEnter += (s, e) => { cellBorder.Background = new SolidColorBrush(Color.FromArgb(40, WeekdayColor.R, WeekdayColor.G, WeekdayColor.B)); };
                cellBorder.MouseLeave += (s, e) => { cellBorder.Background = defaultBg; };

                StackPanel innerPanel = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                TextBlock dayText = new TextBlock
                {
                    Text = day.ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontWeight = isToday ? FontWeights.Bold : FontWeights.Medium,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 0, 4),
                    Foreground = isWeekend ? new SolidColorBrush(WeekendColor) : (Brush)FindResource("DynamicMainText")
                };
                innerPanel.Children.Add(dayText);

                if (monthData.TryGetValue(day, out var data))
                {
                    var trafficColor = GetTrafficColor(data.TotalBytes);

                    Border trafficBox = new Border
                    {
                        Height = 4,
                        Width = 18,
                        CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(trafficColor),
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    innerPanel.Children.Add(trafficBox);

                    cellBorder.ToolTip = App.T("History_CellTooltip", "Date: {0}\nTotal: {1}",
                        new DateTime(currentYear, currentMonth, day).ToString("yyyy-MM-dd"),
                        FormatBytes(data.TotalBytes));

                    monthTotal += data.TotalBytes;
                    monthUp += data.UploadBytes;
                    monthDown += data.DownloadBytes;
                }
                else
                {
                    Border emptyBox = new Border { Height = 4, Width = 18 };
                    innerPanel.Children.Add(emptyBox);
                }

                cellBorder.Child = innerPanel;

                Grid.SetRow(cellBorder, row);
                Grid.SetColumn(cellBorder, col);
                CalendarGrid.Children.Add(cellBorder);

                col++;
                if (col > 6)
                {
                    col = 0;
                    row++;
                }
                if (row > 6) break;
            }

            UpdateMonthSummaryText(monthUp, monthDown, monthTotal);
        }

        private string FormatBytes(long b)
        {
            if (b >= 1099511627776L) return $"{b / 1099511627776.0:F2} TB";
            if (b >= 1073741824L) return $"{b / 1073741824.0:F2} GB";
            if (b >= 1048576L) return $"{b / 1048576.0:F2} MB";
            if (b >= 1024L) return $"{b / 1024.0:F2} KB";
            return $"{b} B";
        }

        private void UpdateMonthSummaryText(long upload, long download, long total)
        {
            if (MonthUploadText != null)
                MonthUploadText.Text = FormatBytes(upload);

            if (MonthDownloadText != null)
                MonthDownloadText.Text = FormatBytes(download);

            if (MonthTotalText != null)
                MonthTotalText.Text = FormatBytes(total);
        }

        private void UpdateCalendarFlowDirection()
        {
            var flowDirection = (FlowDirection)Application.Current.Resources["ApplicationFlowDirection"];
            CalendarGrid.FlowDirection = flowDirection == FlowDirection.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        }

        #endregion

        #region ================== EVENT HANDLERS ==================

        private void ViewTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HistoryList == null) return;
            int idx = ViewTypeCombo.SelectedIndex;
            if (idx == 0)
            {
                allHistory = historyService.GetHistoryFilled(_installDate);
                RefreshList();
            }
            else if (idx == 1) { allHistory = GroupBy(true); RefreshList(); }
            else { allHistory = GroupBy(false); RefreshList(); }
        }

        private List<HistoryData> GroupBy(bool byMonth)
        {
            var raw = historyService.GetHistory();
            if (byMonth)
            {
                return raw.GroupBy(h => new { h.Date.Year, h.Date.Month })
                          .Select(g => new HistoryData
                          {
                              Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                              UploadBytes = g.Sum(x => x.UploadBytes),
                              DownloadBytes = g.Sum(x => x.DownloadBytes)
                          }).OrderByDescending(x => x.Date).ToList();
            }
            return raw.GroupBy(h => h.Date.Year)
                      .Select(g => new HistoryData
                      {
                          Date = new DateTime(g.Key, 1, 1),
                          UploadBytes = g.Sum(x => x.UploadBytes),
                          DownloadBytes = g.Sum(x => x.DownloadBytes)
                      }).OrderByDescending(x => x.Date).ToList();
        }

        private void ScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            logarithmic = ScaleCombo.SelectedIndex == 1;
            RefreshList();
        }

        private void CalendarRefresh(object sender, SelectionChangedEventArgs e)
        {
            if (MonthCombo.SelectedItem is ComboBoxItem item && item.Tag != null)
                currentMonth = (int)item.Tag;

            RefreshCalendar();
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            if (currentYear == _installDate.Year && currentMonth == _installDate.Month) return;

            if (currentMonth == 1)
            {
                currentMonth = 12;
                currentYear--;
            }
            else currentMonth--;

            if (YearCombo.Items.Contains(currentYear))
                YearCombo.SelectedItem = currentYear;

            RefreshMonthCombo(currentYear);

            foreach (ComboBoxItem item in MonthCombo.Items)
            {
                if ((int)item.Tag == currentMonth)
                {
                    MonthCombo.SelectionChanged -= CalendarRefresh;
                    MonthCombo.SelectedItem = item;
                    MonthCombo.SelectionChanged += CalendarRefresh;
                    break;
                }
            }

            RefreshCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            if (currentYear == DateTime.Today.Year && currentMonth == DateTime.Today.Month) return;

            if (currentMonth == 12)
            {
                currentMonth = 1;
                currentYear++;
            }
            else currentMonth++;

            if (YearCombo.Items.Contains(currentYear))
                YearCombo.SelectedItem = currentYear;

            RefreshMonthCombo(currentYear);

            foreach (ComboBoxItem item in MonthCombo.Items)
            {
                if ((int)item.Tag == currentMonth)
                {
                    MonthCombo.SelectionChanged -= CalendarRefresh;
                    MonthCombo.SelectedItem = item;
                    MonthCombo.SelectionChanged += CalendarRefresh;
                    break;
                }
            }

            RefreshCalendar();
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            YearCombo.SelectedItem = DateTime.Today.Year;

            RefreshMonthCombo(DateTime.Today.Year);

            foreach (ComboBoxItem item in MonthCombo.Items)
            {
                if ((int)item.Tag == DateTime.Today.Month)
                {
                    MonthCombo.SelectionChanged -= CalendarRefresh;
                    MonthCombo.SelectedItem = item;
                    MonthCombo.SelectionChanged += CalendarRefresh;
                    break;
                }
            }

            RefreshCalendar();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
            Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ListViewTab_Click(object sender, RoutedEventArgs e)
        {
            ListViewPanel.Visibility = Visibility.Visible;
            CalendarViewPanel.Visibility = Visibility.Collapsed;

            ListViewTab.Style = (Style)FindResource("TabBtnActive");
            CalendarViewTab.Style = (Style)FindResource("TabBtn");
        }

        private void CalendarViewTab_Click(object sender, RoutedEventArgs e)
        {
            ListViewPanel.Visibility = Visibility.Collapsed;
            CalendarViewPanel.Visibility = Visibility.Visible;

            ListViewTab.Style = (Style)FindResource("TabBtn");
            CalendarViewTab.Style = (Style)FindResource("TabBtnActive");

            RefreshCalendar();
        }

        #endregion
    }

    #region ================== HISTORY ROW CLASS ==================

    public class HistoryRow
    {
        public DateTime Date { get; set; }
        public long Upload { get; set; }
        public long Download { get; set; }
        public long Total => Upload + Download;
        public double BarWidth { get; set; }
        public Brush BarBrush { get; set; }

        public string DateText => Date.ToString("yyyy/MM/dd (ddd)", CultureInfo.InvariantCulture);
        public string UploadText => Format(Upload);
        public string DownloadText => Format(Download);
        public string TotalText => Format(Total);

        private string Format(long b)
        {
            if (b >= 1099511627776L) return $"{b / 1099511627776.0:F2} TB";
            if (b >= 1073741824L) return $"{b / 1073741824.0:F2} GB";
            if (b >= 1048576L) return $"{b / 1048576.0:F2} MB";
            if (b >= 1024L) return $"{b / 1024.0:F2} KB";
            return $"{b} B";
        }
    }

    #endregion
}