using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using TrafficMonitor.Models;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace TrafficMonitor
{
    public partial class TaskbarWindow : Window
    {
        private SettingsData settings;
        private System.Windows.Threading.DispatcherTimer positionTimer;
        private bool _isEmbedded = false;

        private const double WindowWidthDip = 95.0;
        private const double WindowHeightDip = 34.0;

        #region ================== UI BUILDER ==================

        private bool _isLightTheme = false;
        private const int WM_SETTINGCHANGE = 0x001A;

        private static readonly SolidColorBrush DarkThemeBrush =
            new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        private static readonly SolidColorBrush LightThemeBrush =
            new SolidColorBrush(Color.FromRgb(0x00, 0x00, 0x00));

        static TaskbarWindow()
        {
            DarkThemeBrush.Freeze();
            LightThemeBrush.Freeze();
        }

        public TaskbarWindow()
        {
            InitializeComponent();
            this.SourceInitialized += TaskbarWindow_SourceInitialized;
            this.Loaded += TaskbarWindow_Loaded;
            this.MouseRightButtonUp += TaskbarWindow_MouseRightButtonUp;
            this.MouseLeftButtonDown += TaskbarWindow_MouseLeftButtonDown;
        }

        #endregion

        #region ================== THEME DETECTION ==================

        private static bool IsWindowsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key == null) return false;

                var val = key.GetValue("SystemUsesLightTheme");
                if (val is int i) return i == 1;
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyTheme()
        {
            bool light = IsWindowsLightTheme();
            _isLightTheme = light;
            RootBorder.Tag = light ? LightThemeBrush : DarkThemeBrush;
            Debug.WriteLine($"Taskbar theme applied: {(light ? "Light" : "Dark")}");
        }

        #endregion

        #region ================== WINDOW INITIALIZATION ==================

        private void TaskbarWindow_SourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            ex |= WS_EX_TOOLWINDOW | WS_EX_LAYERED;
            ex &= ~WS_EX_APPWINDOW;
            SetWindowLong(hwnd, GWL_EXSTYLE, ex);

            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
            ApplyTheme();
        }

        private void TaskbarWindow_Loaded(object sender, RoutedEventArgs e)
        {
            EmbedIntoTaskbar();

            positionTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            positionTimer.Tick += (s, _) =>
            {
                if (!_isEmbedded) EmbedIntoTaskbar();
                else UpdatePosition();
                ApplyTheme();
            };
            positionTimer.Start();
        }

        #endregion

        #region ================== WND PROC HANDLER ==================

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_MOUSEACTIVATE:
                    handled = true;
                    return new IntPtr(MA_NOACTIVATE);

                case WM_WINDOWPOSCHANGING:
                    int ptrSize = IntPtr.Size;
                    int flagsOff = ptrSize * 2 + 16;
                    int flags = Marshal.ReadInt32(lParam, flagsOff);
                    flags |= (int)(SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                    Marshal.WriteInt32(lParam, flagsOff, flags);
                    break;

                case WM_NCHITTEST:
                    handled = true;
                    return new IntPtr(HTCLIENT);

                case WM_SETTINGCHANGE:
                    try
                    {
                        string param = lParam != IntPtr.Zero ? Marshal.PtrToStringUni(lParam) : null;
                        if (string.IsNullOrEmpty(param) || param == "ImmersiveColorSet" || param == "WindowsThemeElement" || param == "Policy")
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                System.Threading.Tasks.Task.Delay(50).ContinueWith(_ =>
                                    Dispatcher.BeginInvoke(new Action(ApplyTheme)));
                            }));
                        }
                    }
                    catch { }
                    break;
            }
            return IntPtr.Zero;
        }

        #endregion

        #region ================== TASKBAR EMBEDDING ==================

        private void EmbedIntoTaskbar()
        {
            try
            {
                IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
                if (taskbar == IntPtr.Zero) return;

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                IntPtr oldParent = SetParent(hwnd, taskbar);
                if (oldParent == IntPtr.Zero)
                {
                    Debug.WriteLine("SetParent failed — using fallback");
                    FallbackPositioning();
                    return;
                }

                int style = GetWindowLong(hwnd, GWL_STYLE);
                style &= ~WS_POPUP;
                style |= WS_CHILD;
                SetWindowLong(hwnd, GWL_STYLE, style);

                _isEmbedded = true;
                Debug.WriteLine("Embedded into taskbar successfully");

                UpdatePosition();
                this.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"EmbedIntoTaskbar: {ex.Message}");
                FallbackPositioning();
            }
        }

        private void UpdatePosition()
        {
            try
            {
                IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
                if (taskbar == IntPtr.Zero) return;
                if (!GetWindowRect(taskbar, out RECT tb)) return;

                int tbH = tb.Bottom - tb.Top;
                int tbW = tb.Right - tb.Left;
                bool horizontal = tbW > tbH;

                double dpiX = 1.0, dpiY = 1.0;
                var src = PresentationSource.FromVisual(this);
                if (src?.CompositionTarget != null)
                {
                    dpiX = src.CompositionTarget.TransformToDevice.M11;
                    dpiY = src.CompositionTarget.TransformToDevice.M22;
                }

                int wPx = (int)(WindowWidthDip * dpiX);
                int hPx = horizontal ? Math.Min(tbH - 2, (int)(WindowHeightDip * dpiY)) : (int)(52 * dpiY);

                int xPx, yPx;
                IntPtr trayWnd = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);

                if (horizontal && trayWnd != IntPtr.Zero && GetWindowRect(trayWnd, out RECT tr))
                {
                    xPx = (tr.Left - tb.Left) - wPx - 2;
                    yPx = (tbH - hPx) / 2;
                }
                else if (horizontal)
                {
                    xPx = tbW - wPx - 4;
                    yPx = (tbH - hPx) / 2;
                }
                else
                {
                    xPx = (tbW - wPx) / 2;
                    yPx = tbH - hPx - 4;
                }

                xPx = Math.Max(0, xPx);
                yPx = Math.Max(0, yPx);

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, IntPtr.Zero, xPx, yPx, wPx, hPx,
                        SWP_NOACTIVATE | SWP_NOZORDER | SWP_SHOWWINDOW);
                }

                this.Width = wPx / dpiX;
                this.Height = hPx / dpiY;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdatePosition: {ex.Message}");
            }
        }

        private void FallbackPositioning()
        {
            try
            {
                IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
                if (taskbar == IntPtr.Zero) return;
                if (!GetWindowRect(taskbar, out RECT tb)) return;

                int tbH = tb.Bottom - tb.Top;
                int tbW = tb.Right - tb.Left;
                bool horizontal = tbW > tbH;

                double dpiX = 1.0, dpiY = 1.0;
                var src = PresentationSource.FromVisual(this);
                if (src?.CompositionTarget != null)
                {
                    dpiX = src.CompositionTarget.TransformToDevice.M11;
                    dpiY = src.CompositionTarget.TransformToDevice.M22;
                }

                double w = WindowWidthDip;
                double h = horizontal ? Math.Min((tbH - 2.0) / dpiY, WindowHeightDip) : 52.0;

                double left, top;
                IntPtr trayWnd = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);

                if (horizontal && trayWnd != IntPtr.Zero && GetWindowRect(trayWnd, out RECT tr))
                {
                    left = tr.Left / dpiX - w - 2.0;
                    top = tb.Top / dpiY + (tbH / dpiY - h) / 2.0;
                }
                else if (horizontal)
                {
                    left = tb.Right / dpiX - w - 4.0;
                    top = tb.Top / dpiY + (tbH / dpiY - h) / 2.0;
                }
                else
                {
                    left = tb.Left / dpiX + (tbW / dpiX - w) / 2.0;
                    top = tb.Bottom / dpiY - h - 4.0;
                }

                this.Left = Math.Max(0, left);
                this.Top = Math.Max(0, top);
                this.Width = w;
                this.Height = h;

                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FallbackPositioning: {ex.Message}");
            }
        }

        #endregion

        #region ================== MOUSE EVENT HANDLERS ==================

        private void TaskbarWindow_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var menu = App.BuildContextMenu();
                menu.PlacementTarget = this;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                menu.IsOpen = true;
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void TaskbarWindow_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2 || settings == null) return;
            switch (settings.TaskbarDoubleClickAction)
            {
                case 0: new Views.NetworkInfoWindow(App.NetworkService.GetConnections()).Show(); break;
                case 1: new Views.HistoryWindow().Show(); break;
                case 2:
                    var opt = new Views.OptionsWindow(settings);
                    if (opt.ShowDialog() == true)
                    {
                        settings = opt.Settings;
                        App.CurrentSettings = settings;
                        App.SettingsService.Save(settings);
                    }
                    break;
            }
        }

        #endregion

        #region ================== PUBLIC API ==================

        public void SetSettings(SettingsData s) => settings = s;

        public void UpdateData(NetworkData data)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UploadText.Text = FormatSpeed(data.UploadSpeed);
                DownloadText.Text = FormatSpeed(data.DownloadSpeed);

                var tooltipTemplate = App.T("Taskbar_Tooltip", "↑ {0}\n↓ {1}\nToday: {2}",
                    data.UploadText, data.DownloadText, data.TodayText);
                ToolTip = tooltipTemplate.Replace("\\n", "\n");
            }));
        }

        #endregion

        #region ================== HELPERS ==================

        private static string FormatSpeed(double bps)
        {
            const double KB = 1024.0;
            const double MB = KB * 1024.0;
            const double GB = MB * 1024.0;

            if (bps < KB)
                return $"{bps / KB:F2} KB/s";

            if (bps < MB)
                return $"{bps / KB:F2} KB/s";

            if (bps < GB)
                return $"{bps / MB:F2} MB/s";

            return $"{bps / GB:F2} GB/s";
        }

        #endregion

        #region ================== CLOSE HANDLER ==================

        protected override void OnClosed(EventArgs e)
        {
            positionTimer?.Stop();

            var hwnd = new WindowInteropHelper(this).Handle;
            if (_isEmbedded && hwnd != IntPtr.Zero)
                SetParent(hwnd, IntPtr.Zero);

            base.OnClosed(e);
        }

        #endregion

        #region ================== WIN32 API ==================

        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_CHILD = 0x40000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;
        private const int WS_EX_LAYERED = 0x00080000;

        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_WINDOWPOSCHANGING = 0x0046;

        private const int MA_NOACTIVATE = 3;
        private const int HTCLIENT = 1;

        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOZORDER = 0x0004;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int n);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int n, int v);
        [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr hWnd, IntPtr hParent);
        [DllImport("user32.dll")] static extern IntPtr FindWindow(string c, string n);
        [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr p, IntPtr c, string cn, string wn);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr h, IntPtr ins, int x, int y, int cx, int cy, uint f);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        #endregion
    }
}