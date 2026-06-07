using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TrafficMonitor.Models;
using TrafficMonitor.Services;
using TrafficMonitor.Themes;
using TrafficMonitor.Views;
using Application = System.Windows.Application;
using FontFamily = System.Windows.Media.FontFamily;

namespace TrafficMonitor
{
    public partial class App : Application
    {
        #region ================== EVENTS ==================

        public static event Action<string> LanguageChanged;
        public static event Action<FlowDirection> FlowDirectionChanged;

        #endregion

        #region ================== STATIC PROPERTIES ==================

        public static TaskbarWindow TaskbarWindowInstance { get; set; }
        public static MainWindow MainWindowInstance { get; set; }
        public static TaskbarIcon TrayIcon { get; private set; }
        public static RegistrySettingsService SettingsService { get; private set; }
        public static SettingsData CurrentSettings { get; set; }
        public static NetworkService NetworkService { get; private set; }

        public static bool IsDarkMode => ThemeManager.IsDarkMode;
        public static bool IsTrayEnabled => _isTrayEnabled;
        public static string CurrentLanguage => _currentLanguage;

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private const string REG_PATH = @"SOFTWARE\TrafficMonitor";

        private static ResourceDictionary _currentLanguageDict;
        private static bool _isChangingLanguage = false;
        private static readonly object _languageLock = new object();
        private static bool _isTrayEnabled = true;
        private static string _currentLanguage = "en";

        private static System.Drawing.Icon _originalTrayIcon;
        private static bool _trayWarningActive = false;

        #endregion

        #region ================== LOCALIZATION HELPER ==================

        public static string T(string key, string fallback = "")
        {
            try
            {
                var value = Current.TryFindResource(key);
                return value?.ToString() ?? (string.IsNullOrEmpty(fallback) ? key : fallback);
            }
            catch { return string.IsNullOrEmpty(fallback) ? key : fallback; }
        }

        public static string T(string key, string fallback, params object[] args)
        {
            try
            {
                var template = T(key, fallback);
                return string.Format(template, args);
            }
            catch { return fallback; }
        }

        #endregion

        #region ================== STARTUP ==================

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            ThemeManager.Initialize();
            LoadApplicationSettings();

            SettingsService = new RegistrySettingsService();
            CurrentSettings = SettingsService.Load();

            NetworkService = new NetworkService();

            NetworkService.TrafficLimitReached += OnTrafficLimitReached;

            CreateTrayIcon();

            MainWindowInstance = new MainWindow();
            MainWindow = MainWindowInstance;

            if (!CurrentSettings.HideMainWindow)
                MainWindowInstance.Show();

            if (CurrentSettings.ShowTaskbarWindow)
            {
                ShowTaskbarWindow();
            }

            NetworkService.DataUpdated += (data) =>
            {
                Dispatcher.Invoke(() =>
                {
                    TaskbarWindowInstance?.UpdateData(data);
                });
            };
        }

        #endregion

        #region ================== LOAD SETTINGS ==================

        private void LoadApplicationSettings()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_PATH))
                {
                    string langCode = key?.GetValue("Language")?.ToString() ?? "en";
                    SetLanguage(langCode);

                    bool hwAccel = Convert.ToBoolean(key?.GetValue("HardwareAccel") ?? true);
                    RenderOptions.ProcessRenderMode = hwAccel
                        ? RenderMode.Default
                        : RenderMode.SoftwareOnly;

                    _isTrayEnabled = Convert.ToBoolean(key?.GetValue("TrayEnabled") ?? true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load Settings Error: {ex.Message}");
                _isTrayEnabled = true;
            }
        }

        #endregion

        #region ================== LANGUAGE METHODS ==================

        public static void SetLanguage(string languageCode)
        {
            lock (_languageLock)
            {
                if (_isChangingLanguage) return;
                _isChangingLanguage = true;
            }

            try
            {
                Current.Dispatcher.Invoke(async () =>
                {
                    try
                    {
                        var resources = Current.Resources;
                        if (_currentLanguageDict != null)
                        {
                            resources.MergedDictionaries.Remove(_currentLanguageDict);
                            _currentLanguageDict = null;
                        }

                        await Task.Delay(50);

                        string langFile = languageCode switch
                        {
                            "ar" => "Lang/Lang-AR.xaml",
                            "fr" => "Lang/Lang-FR.xaml",
                            "es" => "Lang/Lang-ES.xaml",
                            "ru" => "Lang/Lang-RU.xaml",
                            _ => "Lang/Lang-EN.xaml"
                        };

                        _currentLanguageDict = new ResourceDictionary
                        {
                            Source = new Uri(langFile, UriKind.Relative)
                        };
                        resources.MergedDictionaries.Add(_currentLanguageDict);

                        await Task.Delay(50);
                        ApplyLanguageFont(languageCode);

                        CultureInfo culture;
                        if (languageCode == "ar")
                        {
                            culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
                            culture.NumberFormat = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                            culture.DateTimeFormat = (DateTimeFormatInfo)CultureInfo.InvariantCulture.DateTimeFormat.Clone();
                            culture.DateTimeFormat.Calendar = new GregorianCalendar();
                            culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
                        }
                        else
                        {
                            culture = new CultureInfo(languageCode switch
                            {
                                "fr" => "fr-FR",
                                "es" => "es-ES",
                                "ru" => "ru-RU",
                                _ => "en-US"
                            });
                        }

                        Thread.CurrentThread.CurrentCulture = culture;
                        Thread.CurrentThread.CurrentUICulture = culture;

                        FlowDirection newFlowDirection = languageCode == "ar"
                            ? FlowDirection.RightToLeft
                            : FlowDirection.LeftToRight;
                        resources["ApplicationFlowDirection"] = newFlowDirection;

                        foreach (Window window in Current.Windows)
                        {
                            if (window?.Content is FrameworkElement content)
                                content.FlowDirection = newFlowDirection;
                        }

                        using (var key = Registry.CurrentUser.CreateSubKey(REG_PATH))
                        {
                            key?.SetValue("Language", languageCode);
                        }

                        _currentLanguage = languageCode;

                        await Task.Delay(100);

                        RefreshMenu();

                        ToastManager.RefreshFlowDirection();

                        LanguageChanged?.Invoke(languageCode);
                        FlowDirectionChanged?.Invoke(newFlowDirection);
                    }
                    finally
                    {
                        _isChangingLanguage = false;
                    }
                }, DispatcherPriority.Normal);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetLanguage Error: {ex.Message}");
                _isChangingLanguage = false;
            }
        }

        public static void ApplyLanguageFont(string langCode)
        {
            try
            {
                string resourceKey = langCode switch
                {
                    "ar" => "Font_AR",
                    "fr" => "Font_FR",
                    "es" => "Font_ES",
                    "ru" => "Font_RU",
                    _ => "Font_EN"
                };

                var font = Current.TryFindResource(resourceKey) as FontFamily;
                if (font != null)
                {
                    Current.Resources["GlobalFontFamily"] = font;

                    Debug.WriteLine($"✅ Font Source: {font.Source}");
                    foreach (var familyName in font.FamilyNames)
                    {
                        Debug.WriteLine($"   FamilyName: {familyName.Key} => {familyName.Value}");
                    }

                    foreach (var typeface in font.GetTypefaces())
                    {
                        if (typeface.TryGetGlyphTypeface(out var glyph))
                        {
                            Debug.WriteLine($"   Glyph: {glyph.FamilyNames.Values.FirstOrDefault()}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"❌ Font key not found: {resourceKey}");
                    Current.Resources["GlobalFontFamily"] = new FontFamily("Segoe UI");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Font error: {ex.Message}");
            }
        }

        #endregion

        #region ================== TRAY ICON ==================

        private void CreateTrayIcon()
        {
            TrayIcon = new TaskbarIcon();
            TrayIcon.ToolTipText = T("Tray_DefaultTooltip", "Traffic Monitor");

            try
            {
                System.Drawing.Icon iconToUse = null;

                try
                {
                    var iconUri = new Uri("pack://application:,,,/Assets/Icons/icon.ico", UriKind.Absolute);
                    var streamInfo = Application.GetResourceStream(iconUri);
                    if (streamInfo != null)
                    {
                        using var stream = streamInfo.Stream;
                        iconToUse = new System.Drawing.Icon(stream);
                    }
                }
                catch { }

                if (iconToUse == null)
                {
                    var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exePath))
                        iconToUse = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                }

                if (iconToUse == null)
                    iconToUse = System.Drawing.SystemIcons.Application;

                TrayIcon.Icon = iconToUse;
                _originalTrayIcon = iconToUse;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Tray icon load error: {ex.Message}");
                try { TrayIcon.Icon = System.Drawing.SystemIcons.Application; _originalTrayIcon = System.Drawing.SystemIcons.Application; } catch { }
            }

            TrayIcon.ContextMenu = BuildContextMenu();
            TrayIcon.TrayMouseDoubleClick += (s, args) => ToggleMainWindow();

            if (!_isTrayEnabled)
                TrayIcon.Visibility = Visibility.Collapsed;
        }

        private static void OnTrafficLimitReached(long totalBytes, long limitBytes)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    string totalFormatted = FormatBytesTraffic(totalBytes);
                    string limitFormatted = FormatBytesTraffic(limitBytes);

                    ToastManager.Warning("Toast_TrafficLimitReached", totalFormatted, limitFormatted);

                    SetTrayIconWarning(true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnTrafficLimitReached error: {ex.Message}");
                }
            });
        }

        private static void SetTrayIconWarning(bool warning)
        {
            try
            {
                if (TrayIcon == null) return;

                if (warning && !_trayWarningActive)
                {
                    _originalTrayIcon = TrayIcon.Icon;
                    _trayWarningActive = true;

                    TrayIcon.Icon = CreateWarningIcon();
                    TrayIcon.ToolTipText = T("Tray_LimitTooltip", "⚠️ Traffic Monitor - Daily limit reached!");
                }
                else if (!warning && _trayWarningActive)
                {
                    if (_originalTrayIcon != null)
                        TrayIcon.Icon = _originalTrayIcon;
                    _trayWarningActive = false;
                    TrayIcon.ToolTipText = T("Tray_DefaultTooltip", "Traffic Monitor");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetTrayIconWarning error: {ex.Message}");
            }
        }

        public static void ResetTrayIconWarning()
        {
            SetTrayIconWarning(false);
        }

        private static System.Drawing.Icon CreateWarningIcon()
        {
            try
            {
                var bmp = new System.Drawing.Bitmap(32, 32);
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.Clear(System.Drawing.Color.Transparent);

                    var triangle = new System.Drawing.Point[]
                    {
                        new System.Drawing.Point(16, 2),
                        new System.Drawing.Point(30, 28),
                        new System.Drawing.Point(2, 28)
                    };

                    using (var brush = new System.Drawing.SolidBrush(
                        System.Drawing.Color.FromArgb(255, 245, 158, 11)))
                        g.FillPolygon(brush, triangle);

                    using (var pen = new System.Drawing.Pen(
                        System.Drawing.Color.FromArgb(255, 180, 100, 0), 1.5f))
                        g.DrawPolygon(pen, triangle);

                    using (var brush = new System.Drawing.SolidBrush(
                        System.Drawing.Color.FromArgb(255, 30, 30, 30)))
                    {
                        g.FillRectangle(brush, 14, 10, 4, 10);
                        g.FillEllipse(brush, 14, 22, 4, 4);
                    }
                }

                return System.Drawing.Icon.FromHandle(bmp.GetHicon());
            }
            catch
            {
                return System.Drawing.SystemIcons.Warning;
            }
        }

        private static string FormatBytesTraffic(long bytes)
        {
            const double GB = 1024.0 * 1024 * 1024;
            const double MB = 1024.0 * 1024;
            const double KB = 1024.0;

            if (bytes >= GB) return $"{bytes / GB:F2} GB";
            if (bytes >= MB) return $"{bytes / MB:F2} MB";
            if (bytes >= KB) return $"{bytes / KB:F2} KB";
            return $"{bytes} B";
        }

        #endregion

        #region ================== CONTEXT MENU ==================

        public static ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu();

            var autoCloseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };

            menu.Opened += (s, e) =>
            {
                autoCloseTimer.Stop();
                autoCloseTimer.Start();
            };

            menu.MouseMove += (s, e) =>
            {
                autoCloseTimer.Stop();
                autoCloseTimer.Start();
            };

            autoCloseTimer.Tick += (s, e) =>
            {
                autoCloseTimer.Stop();
                menu.IsOpen = false;
            };

            menu.Closed += (s, e) => autoCloseTimer.Stop();

            //--------------- Connections Menu ---------------
            var connectionsMenu = CreateMenuItem(
                T("Menu_SelectNetworkConnections", "Select Network Connections"),
                "\uE839", null);

            var autoItem = CreateMenuItem(
                T("Menu_AutoSelect", "Auto Select"),
                null, (s, e) => SetAutoSelect(true));
            autoItem.IsChecked = CurrentSettings?.AutoSelect ?? true;
            connectionsMenu.Items.Add(autoItem);
            connectionsMenu.Items.Add(new Separator());

            if (NetworkService != null)
            {
                var connections = NetworkService.GetConnections();
                foreach (var conn in connections)
                {
                    var connName = conn.Name;
                    var connType = conn.Type;
                    var displayName = $"{connName} ({connType})";

                    var item = CreateMenuItem(displayName, conn.TypeIcon,
                        (s, e) => SwitchConnection(connName));

                    if (CurrentSettings?.SelectedConnection == connName)
                        item.IsChecked = true;

                    if (!conn.IsActive)
                        item.IsEnabled = false;

                    connectionsMenu.Items.Add(item);
                }
            }
            menu.Items.Add(connectionsMenu);

            //--------------- Main Menu Items ---------------
            menu.Items.Add(CreateMenuItem(
                T("Menu_ConnectionDetails", "Connection Details"),
                "\uE946", (s, e) => ShowNetworkInfo()));

            menu.Items.Add(CreateMenuItem(
                T("Menu_HistoricalTraffic", "Historical Traffic Statistics"),
                "\uE9D9", (s, e) => ShowHistory()));

            menu.Items.Add(CreateMenuItem(
                T("Menu_ProcessNetworkUsage", "Process Network Usage"),
                "\uF156", (s, e) => ShowProcessNetwork()));

            menu.Items.Add(CreateMenuItem(
                T("Menu_SpeedTest", "Speed Test"),
                "\uE704", (s, e) => ShowSpeedTest()));

            menu.Items.Add(CreateMenuItem(
                T("Menu_Options", "Options..."),
                "\uE115", (s, e) => ShowOptions()));

            menu.Items.Add(CreateMenuItem(
                T("Menu_ShowMainWindow", "Show Main Window"),
                "\uE740", (s, e) => ToggleMainWindow()));

            //--------------- Taskbar Window Toggle ---------------
            var taskbarVisible = TaskbarWindowInstance != null;
            var taskbarToggleItem = CreateMenuItem(
                taskbarVisible
                    ? T("Menu_CloseTaskbarWindow", "Close Taskbar Window")
                    : T("Menu_ShowTaskbarWindow", "Show Taskbar Window"),
                taskbarVisible ? "\uE8BB" : "\uE718",
                (s, e) => ToggleTaskbarWindow());
            menu.Items.Add(taskbarToggleItem);

            menu.Items.Add(CreateMenuItem(
                T("Menu_TaskManager", "Task Manager"),
                "\uE7C4", (s, e) => OpenTaskManager()));

            //--------------- Help Menu ---------------
            var helpMenu = CreateMenuItem(T("Menu_Help", "Help"), "\uE897", null);
            helpMenu.Items.Add(CreateMenuItem(
                T("Menu_About", "About..."),
                "\uE946", (s, e) => ShowAbout()));
            menu.Items.Add(helpMenu);

            menu.Items.Add(new Separator());

            menu.Items.Add(CreateMenuItem(
                T("Menu_Exit", "Exit"),
                "\uE7E8", (s, e) => ExitApp()));

            return menu;
        }

        private static MenuItem CreateMenuItem(string header, string iconGlyph, RoutedEventHandler clickHandler)
        {
            var item = new MenuItem
            {
                Header = header,
                Padding = new Thickness(8, 6, 15, 6)
            };

            if (!string.IsNullOrEmpty(iconGlyph))
            {
                item.Icon = new TextBlock
                {
                    Text = iconGlyph,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
            }

            if (clickHandler != null)
                item.Click += clickHandler;
            return item;
        }

        private static MenuItem CreateMenuItem(string header, RoutedEventHandler clickHandler)
        {
            var item = new MenuItem
            {
                Header = header,
                Padding = new Thickness(15, 6, 15, 6)
            };
            if (clickHandler != null)
                item.Click += clickHandler;
            return item;
        }

        public static void RefreshMenu()
        {
            if (TrayIcon != null)
            {
                Current.Dispatcher.Invoke(() =>
                {
                    TrayIcon.ContextMenu = BuildContextMenu();
                });
            }
        }

        #endregion

        #region ================== NETWORK ACTIONS ==================

        private static void SetAutoSelect(bool auto)
        {
            if (CurrentSettings != null)
            {
                CurrentSettings.AutoSelect = auto;
                SettingsService?.Save(CurrentSettings);
                RefreshMenu();
            }
        }

        private static void SwitchConnection(string name)
        {
            if (CurrentSettings != null && NetworkService != null)
            {
                CurrentSettings.AutoSelect = false;
                CurrentSettings.SelectedConnection = name;
                NetworkService.SwitchConnection(name);
                SettingsService?.Save(CurrentSettings);
                RefreshMenu();
            }
        }

        #endregion

        #region ================== WINDOW MANAGEMENT ==================

        public static void ToggleMainWindow()
        {
            if (MainWindowInstance == null || !MainWindowInstance.IsLoaded)
            {
                MainWindowInstance = new MainWindow();
                MainWindowInstance.Show();
                return;
            }

            if (MainWindowInstance.IsVisible)
            {
                MainWindowInstance.Hide();
                if (CurrentSettings != null)
                    CurrentSettings.HideMainWindow = true;
            }
            else
            {
                MainWindowInstance.Show();
                MainWindowInstance.Activate();
                if (CurrentSettings != null)
                    CurrentSettings.HideMainWindow = false;
            }
            SettingsService?.Save(CurrentSettings);
        }

        public static void ShowTaskbarWindow()
        {
            if (TaskbarWindowInstance == null)
            {
                TaskbarWindowInstance = new TaskbarWindow();
                TaskbarWindowInstance.SetSettings(CurrentSettings);
                TaskbarWindowInstance.Show();
                if (CurrentSettings != null)
                {
                    CurrentSettings.ShowTaskbarWindow = true;
                    SettingsService?.Save(CurrentSettings);
                }
            }
            else
            {
                TaskbarWindowInstance.Activate();
            }
        }

        public static void CloseTaskbarWindow()
        {
            if (TaskbarWindowInstance != null)
            {
                TaskbarWindowInstance.Close();
                TaskbarWindowInstance = null;
                if (CurrentSettings != null)
                {
                    CurrentSettings.ShowTaskbarWindow = false;
                    SettingsService?.Save(CurrentSettings);
                }
            }
        }

        public static void ToggleTaskbarWindow()
        {
            if (TaskbarWindowInstance == null)
                ShowTaskbarWindow();
            else
                CloseTaskbarWindow();
            RefreshMenu();
        }

        public static void SetTrayEnabled(bool enabled)
        {
            _isTrayEnabled = enabled;

            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(REG_PATH))
                {
                    key?.SetValue("TrayEnabled", enabled);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetTrayEnabled error: {ex.Message}");
            }

            if (TrayIcon != null)
                TrayIcon.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region ================== DIALOG SHORTCUTS ==================

        private static void OpenTaskManager()
        {
            try { Process.Start("taskmgr.exe"); } catch { }
        }

        private static void ShowNetworkInfo()
        {
            try
            {
                if (NetworkService == null) return;
                var connections = NetworkService.GetConnections();
                var win = new Views.NetworkInfoWindow(connections);
                SetOwnerSafely(win);
                win.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowNetworkInfo error: {ex.Message}");
                MessageBox.Show(T("Common_Error", "Error") + $": {ex.Message}",
                    T("Common_Error", "Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ShowSpeedTest()
        {
            try
            {
                var win = new Views.SpeedTestWindow();
                SetOwnerSafely(win);
                win.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowSpeedTest error: {ex.Message}");
            }
        }

        private static void ShowHistory()
        {
            try
            {
                var historyWindow = new HistoryWindow();
                SetOwnerSafely(historyWindow);
                historyWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowHistory error: {ex.Message}");
                MessageBox.Show(T("Common_Error", "Error") + $": {ex.Message}",
                    T("Common_Error", "Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ShowProcessNetwork()
        {
            try
            {
                var win = new Views.ProcessNetworkWindow();
                SetOwnerSafely(win);
                win.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowProcessNetwork error: {ex.Message}");
            }
        }

        private static void ShowOptions()
        {
            try
            {
                if (CurrentSettings == null) return;

                var window = new OptionsWindow(CurrentSettings);
                SetOwnerSafely(window);
                if (window.ShowDialog() == true)
                {
                    int oldInterval = CurrentSettings.UpdateInterval;
                    CurrentSettings = window.Settings;
                    SettingsService?.Save(CurrentSettings);

                    if (oldInterval != CurrentSettings.UpdateInterval && NetworkService != null)
                    {
                        NetworkService.Stop();
                        NetworkService.Start();
                    }

                    if (!CurrentSettings.AutoSelect &&
                        !string.IsNullOrEmpty(CurrentSettings.SelectedConnection))
                    {
                        NetworkService?.SwitchConnection(CurrentSettings.SelectedConnection);
                    }

                    SetTrayEnabled(CurrentSettings.ShowTrayIcon);

                    MainWindowInstance?.ApplySettings();
                    TaskbarWindowInstance?.SetSettings(CurrentSettings);

                    ThemeManager.SetThemeByMode(CurrentSettings.ThemeMode);

                    RefreshMenu();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowOptions error: {ex.Message}");
                MessageBox.Show(T("Common_Error", "Error") + $": {ex.Message}",
                    T("Common_Error", "Error"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ShowAbout()
        {
            try
            {
                var aboutWindow = new AboutWindow();
                SetOwnerSafely(aboutWindow);
                aboutWindow.Show();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShowAbout error: {ex.Message}");
                MessageBox.Show(T("About_FallbackMessage", "About Traffic Monitor\nVersion 1.0\n\nA network traffic monitoring tool"),
                    T("Menu_About", "About"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region ================== EXIT & CLOSE ==================

        private static void ExitApp()
        {
            try
            {
                SettingsService?.Save(CurrentSettings);
                NetworkService?.GetHistoryService()?.SaveImmediate();
                NetworkService?.Dispose();
                TaskbarWindowInstance?.Close();

                TrayIcon?.Dispose();
                Current.Shutdown();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ExitApp error: {ex.Message}");
                try { NetworkService?.GetHistoryService()?.SaveImmediate(); } catch { }
                Environment.Exit(0);
            }
        }

        private static void SetOwnerSafely(Window win)
        {
            try
            {
                if (MainWindowInstance != null && MainWindowInstance.IsVisible)
                {
                    win.Owner = MainWindowInstance;
                }
                else
                {
                    win.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetOwnerSafely error: {ex.Message}");
            }
        }
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                NetworkService?.GetHistoryService()?.SaveImmediate();
                TrayIcon?.Dispose();
            }
            catch { }
            base.OnExit(e);
        }

        #endregion
    }
}