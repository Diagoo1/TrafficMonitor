using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using TrafficMonitor.Models;
using TrafficMonitor.Services;
using TrafficMonitor.Themes;
using Button = System.Windows.Controls.Button;

namespace TrafficMonitor
{
    public partial class MainWindow : Window
    {
        #region ================== WIN32 IMPORTS ==================

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private NetworkService networkService;
        private SettingsData settings;
        private bool _isDarkMode = false;

        #endregion

        #region ================== CONSTRUCTOR ==================

        public MainWindow()
        {
            InitializeComponent();

            settings = App.CurrentSettings;
            networkService = App.NetworkService;
            networkService.DataUpdated += OnDataUpdated;

            App.LanguageChanged += OnLanguageChanged;
            ThemeManager.ThemeChanged += OnThemeChanged;
            ThemeManager.OpacityChanged += OnOpacityChanged;

            this.Loaded += OnPageLoaded;
            this.Closed += OnPageClosed;
            this.MouseRightButtonUp += MainWindow_MouseRightButtonUp;

            LoadPosition();
        }

        #endregion

        #region ================== EVENT HANDLERS ==================

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _isDarkMode = ThemeManager.IsDarkMode;
                ApplyTheme();
                ApplySavedOpacity();
                ApplySettings();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MainWindow load error: {ex.Message}");
            }
        }

        private void OnPageClosed(object sender, EventArgs e)
        {
            if (networkService != null)
                networkService.DataUpdated -= OnDataUpdated;

            App.LanguageChanged -= OnLanguageChanged;
            ThemeManager.ThemeChanged -= OnThemeChanged;
            ThemeManager.OpacityChanged -= OnOpacityChanged;
        }

        private void OnLanguageChanged(string langCode)
        {
            Dispatcher.Invoke(() => { });
        }

        private void OnThemeChanged(bool isDark)
        {
            if (!this.IsLoaded && this.Visibility != Visibility.Visible)
                return;

            Dispatcher.Invoke(() =>
            {
                if (this.IsLoaded || this.Visibility == Visibility.Visible)
                {
                    _isDarkMode = isDark;
                    ApplyTheme();
                    ApplyWindowBackground();
                }
            }, DispatcherPriority.Background);
        }

        private void OnOpacityChanged(double opacity)
        {
            if (!this.IsLoaded && this.Visibility != Visibility.Visible)
                return;

            Dispatcher.Invoke(() =>
            {
                if (this.IsLoaded || this.Visibility == Visibility.Visible)
                {
                    this.Opacity = opacity;
                }
            }, DispatcherPriority.Background);
        }

        #endregion

        #region ================== THEME & OPACITY ==================

        private void ApplyTheme()
        {
            try
            {
                ThemeManager.ApplyThemeToWindow(this);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyTheme error: {ex.Message}");
            }
        }

        private void ApplyWindowBackground()
        {
            if (settings == null) return;
            if (settings.WindowBgTransparent)
                MainBorder.Background = System.Windows.Media.Brushes.Transparent;
            else
                MainBorder.Background = (System.Windows.Media.Brush)
                    TryFindResource("DynamicWindowBg");
        }

        private void ApplySavedOpacity()
        {
            try
            {
                double opacity = ThemeManager.GetSavedOpacity();
                this.Opacity = opacity;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplySavedOpacity error: {ex.Message}");
            }
        }

        #endregion

        #region ================== DATA UPDATE ==================

        private void OnDataUpdated(NetworkData data)
        {
            Dispatcher.Invoke(() =>
            {
                if (settings.SwapUpDown)
                {
                    UploadText.Text = data.DownloadText;
                    DownloadText.Text = data.UploadText;
                }
                else
                {
                    UploadText.Text = data.UploadText;
                    DownloadText.Text = data.DownloadText;
                }
                TodayText.Text = data.TodayText;

                string typeLabel = data.ActiveConnectionType ?? App.T("ConnType_Network", "Network");
                ConnectionText.Text = typeLabel;
                if (ConnectionBadgeText != null) ConnectionBadgeText.Text = typeLabel;
                if (ConnectionBadgeIcon != null) ConnectionBadgeIcon.Text = data.ConnectionTypeIcon;

                App.TaskbarWindowInstance?.UpdateData(data);

                if (App.TrayIcon != null)
                    App.TrayIcon.ToolTipText = string.Format(
                   "↑ {0}\n↓ {1}\nToday: {2}",
                        data.UploadText,
                        data.DownloadText,
                       data.TodayText);
            });
        }

        #endregion

        #region ================== SETTINGS ==================

        public void ApplySettings()
        {
            settings = App.CurrentSettings;
            this.Topmost = settings.AlwaysOnTop;

            if (settings.WindowBgTransparent)
            {
                MainBorder.Background = System.Windows.Media.Brushes.Transparent;
            }
            else
            {
                MainBorder.Background = (System.Windows.Media.Brush)
                    TryFindResource("DynamicWindowBg");
            }

            if (settings.MousePenetrate)
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
                }
            }
            else
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
                }
            }
        }

        private void LoadPosition()
        {
            if (settings.WindowLeft > 0) this.Left = settings.WindowLeft;
            if (settings.WindowTop > 0) this.Top = settings.WindowTop;
            this.Width = settings.WindowWidth;
            this.Height = settings.WindowHeight;
        }

        private void SavePosition()
        {
            if (settings != null && !settings.LockWindowPos)
            {
                settings.WindowLeft = (int)this.Left;
                settings.WindowTop = (int)this.Top;
                settings.WindowWidth = (int)this.Width;
                settings.WindowHeight = (int)this.Height;
                App.SettingsService.Save(settings);
            }
        }

        #endregion

        #region ================== MOUSE & CLICK HANDLERS ==================

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (settings == null || settings.LockWindowPos) return;

            if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (settings == null || settings.LockWindowPos) return;
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
                this.DragMove();
        }

        private void MainWindow_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var menu = App.BuildContextMenu();
            menu.PlacementTarget = this;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            menu.IsOpen = true;
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button button)) return;

            var menu = App.BuildContextMenu();
            menu.PlacementTarget = button;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SavePosition();
            this.Hide();
            settings.HideMainWindow = true;
            App.SettingsService.Save(settings);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SavePosition();
            base.OnClosing(e);
        }

        #endregion
    }
}