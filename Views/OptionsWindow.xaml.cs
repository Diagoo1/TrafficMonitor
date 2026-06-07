using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using TrafficMonitor.Models;
using TrafficMonitor.Themes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WinColorDialog = System.Windows.Forms.ColorDialog;
using WinFontDialog = System.Windows.Forms.FontDialog;

namespace TrafficMonitor.Views
{
    public partial class OptionsWindow : Window
    {
        #region ================== PROPERTIES ==================

        public SettingsData Settings { get; private set; }

        private const string REG_PATH = @"SOFTWARE\TrafficMonitor";
        private bool _isLoading = true;

        // ✅ Timer for opacity throttling to prevent stuttering
        private System.Windows.Threading.DispatcherTimer _opacityTimer;

        #endregion

        #region ================== CONSTRUCTOR ==================

        public OptionsWindow(SettingsData currentSettings)
        {
            InitializeComponent();

            Settings = currentSettings?.Clone() ?? new SettingsData();

            this.Loaded += OnLoaded;

            // ✅ Subscribe to OpacityChanged event
            ThemeManager.ThemeChanged += OnThemeChanged;
            ThemeManager.OpacityChanged += OnOpacityChanged;
            App.LanguageChanged += OnLanguageChanged;
            this.Closed += (s, e) =>
            {
                ThemeManager.ThemeChanged -= OnThemeChanged;
                ThemeManager.OpacityChanged -= OnOpacityChanged;
                App.LanguageChanged -= OnLanguageChanged;
            };
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ThemeManager.ApplyThemeToWindow(this);
            LoadSettingsToUI();
            _isLoading = false;
        }

        private void OnThemeChanged(bool isDark)
        {
            Dispatcher.Invoke(() => ThemeManager.ApplyThemeToWindow(this));
        }

        // ✅ NEW METHOD: Handles opacity changes from ThemeManager
        private void OnOpacityChanged(double opacity)
        {
            Dispatcher.Invoke(() => this.Opacity = opacity);
        }

        private void OnLanguageChanged(string lang) { }

        #endregion

        #region ================== LOAD UI FROM SETTINGS ==================

        private void LoadSettingsToUI()
        {
            try
            {
                //--------------- Main Window ---------------
                FontNameText.Text = Settings.FontName ?? "Segoe UI";
                FontSizeBox.Text = Settings.FontSize.ToString();
                TextColorBtn.Background = ColorFromHex(Settings.TextColorHex ?? "#FFFFFF");

                BgColorBtn.Background = ColorFromHex(Settings.BackColorHex ?? "#FF2D2D2D");

                if (BgTransparentCheck != null)
                {
                    BgTransparentCheck.IsChecked = Settings.WindowBgTransparent;
                    if (BgColorBtn != null)
                        BgColorBtn.IsEnabled = !Settings.WindowBgTransparent;
                }

                SwapUpDownCheck.IsChecked = Settings.SwapUpDown;
                SpeedShortModeCheck.IsChecked = Settings.SpeedShortMode;
                SeparateUnitCheck.IsChecked = Settings.SeparateValueUnit;
                ShowToolTipCheck.IsChecked = Settings.ShowToolTip;

                AlwaysOnTopCheck.IsChecked = Settings.AlwaysOnTop;
                LockWindowPosCheck.IsChecked = Settings.LockWindowPos;
                HideMainWindowCheck.IsChecked = Settings.HideMainWindowFullscreen;
                MousePenetrateCheck.IsChecked = Settings.MousePenetrate;

                DoubleClickCombo.SelectedIndex = Settings.DoubleClickAction;

                OpacitySlider.Value = Settings.WindowOpacity;

                UnitByteRadio.IsChecked = Settings.UnitByte;
                UnitBitRadio.IsChecked = !Settings.UnitByte;
                SpeedUnitCombo.SelectedIndex = Settings.SpeedUnit;

                //--------------- Taskbar Window ---------------
                ShowTaskbarCheck.IsChecked = Settings.ShowTaskbarWindow;
                TaskbarOffsetXBox.Text = Settings.TaskbarOffsetX.ToString();
                TaskbarOffsetYBox.Text = Settings.TaskbarOffsetY.ToString();
                TaskbarSwapUpDownCheck.IsChecked = Settings.TaskbarSwapUpDown;
                TaskbarShortModeCheck.IsChecked = Settings.TaskbarShortMode;
                TaskbarShowArrowsCheck.IsChecked = Settings.TaskbarShowArrows;

                TaskbarOnLeftCheck.IsChecked = Settings.TaskbarOnLeft;
                TaskbarSeparateUnitCheck.IsChecked = Settings.TaskbarSeparateValueUnit;
                TaskbarDoubleClickCombo.SelectedIndex = Settings.TaskbarDoubleClickAction;
                DigitsNumberCombo.SelectedIndex = Settings.DigitsNumber == 3 ? 0 :
                                                  Settings.DigitsNumber == 5 ? 2 : 1;

                //--------------- General ---------------
                switch (Settings.ThemeMode)
                {
                    case 0: ThemeLightRadio.IsChecked = true; break;
                    case 1: ThemeDarkRadio.IsChecked = true; break;
                    default: ThemeAutoRadio.IsChecked = true; break;
                }

                string lang = App.CurrentLanguage ?? "en";
                foreach (ComboBoxItem item in LanguageCombo.Items)
                {
                    if ((item.Tag?.ToString() ?? "") == lang)
                    {
                        LanguageCombo.SelectedItem = item;
                        break;
                    }
                }

                StartWithWindowsCheck.IsChecked = Settings.StartWithWindows;
                MinimizedStartCheck.IsChecked = Settings.StartMinimized;
                ShowTrayIconCheck.IsChecked = Settings.ShowTrayIcon;
                HardwareAccelCheck.IsChecked = Settings.HardwareAcceleration;

                UpdateIntervalBox.Text = Settings.UpdateInterval.ToString();
                AutoSelectConnCheck.IsChecked = Settings.AutoSelect;

                TrafficTipCheck.IsChecked = Settings.TrafficTipEnable;
                TrafficTipValueBox.Text = Settings.TrafficTipValue.ToString();
                TrafficTipUnitCombo.SelectedIndex = Settings.TrafficTipUnit;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadSettingsToUI error: {ex.Message}");
            }
        }

        #endregion

        #region ================== TAB SWITCHING ==================

        private void MainWindowTab_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel(MainWindowPanel);
            SetActiveTab(MainWindowTab);
        }

        private void TaskbarWindowTab_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel(TaskbarWindowPanel);
            SetActiveTab(TaskbarWindowTab);
        }

        private void GeneralTab_Click(object sender, RoutedEventArgs e)
        {
            ShowPanel(GeneralPanel);
            SetActiveTab(GeneralTab);
        }

        private void ShowPanel(StackPanel target)
        {
            MainWindowPanel.Visibility = Visibility.Collapsed;
            TaskbarWindowPanel.Visibility = Visibility.Collapsed;
            GeneralPanel.Visibility = Visibility.Collapsed;
            target.Visibility = Visibility.Visible;
        }

        private void SetActiveTab(WpfButton activeBtn)
        {
            var inactive = (Style)FindResource("TabBtn");
            var active = (Style)FindResource("TabBtnActive");

            MainWindowTab.Style = inactive;
            TaskbarWindowTab.Style = inactive;
            GeneralTab.Style = inactive;
            activeBtn.Style = active;
        }

        #endregion

        #region ================== FONT & COLOR PICKERS ==================

        private void ChooseFont_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dlg = new WinFontDialog())
                {
                    dlg.Font = new System.Drawing.Font(
                        Settings.FontName ?? "Segoe UI",
                        Settings.FontSize > 0 ? Settings.FontSize : 10f);

                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        FontNameText.Text = dlg.Font.Name;
                        FontSizeBox.Text = ((int)dlg.Font.Size).ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ChooseFont error: {ex.Message}");
            }
        }

        private void RestoreFontDefault_Click(object sender, RoutedEventArgs e)
        {
            FontNameText.Text = "Segoe UI";
            FontSizeBox.Text = "10";
        }

        private void TextColor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dlg = new WinColorDialog())
                {
                    var current = ColorFromHex(Settings.TextColorHex ?? "#FFFFFF");
                    dlg.Color = System.Drawing.Color.FromArgb(current.Color.R, current.Color.G, current.Color.B);

                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var c = dlg.Color;
                        TextColorBtn.Background = new WpfBrush(WpfColor.FromRgb(c.R, c.G, c.B));
                        Settings.TextColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TextColor error: {ex.Message}");
            }
        }

        private void BgColor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var dlg = new WinColorDialog())
                {
                    var current = ColorFromHex(Settings.BackColorHex ?? "#FF2D2D2D");
                    dlg.Color = System.Drawing.Color.FromArgb(current.Color.R, current.Color.G, current.Color.B);

                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        var c = dlg.Color;
                        BgColorBtn.Background = new WpfBrush(WpfColor.FromRgb(c.R, c.G, c.B));
                        Settings.BackColorHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BgColor error: {ex.Message}");
            }
        }

        private void BgTransparentCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            if (BgTransparentCheck == null) return;

            bool transparent = BgTransparentCheck.IsChecked == true;
            if (BgColorBtn != null)
                BgColorBtn.IsEnabled = !transparent;

            Settings.WindowBgTransparent = transparent;
        }

        private void SelectConnection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new NetworkInfoWindow(App.NetworkService.GetConnections());
                win.Owner = this;
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SelectConnection error: {ex.Message}");
                MessageBox.Show($"Error opening network connections: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region ================== OPACITY & THEME & LANGUAGE ==================

        // ✅ MODIFIED: Throttled opacity change to prevent stuttering
        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;

            // Throttle - wait 50ms before applying to avoid sending too many updates
            _opacityTimer?.Stop();
            _opacityTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _opacityTimer.Tick += (s, _) =>
            {
                _opacityTimer.Stop();
                double newOpacity = OpacitySlider.Value;
                ThemeManager.NotifyOpacityChanged(newOpacity);

                // Apply immediately to current window for instant feedback
                this.Opacity = newOpacity;
            };
            _opacityTimer.Start();
        }

        private void Theme_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;

            if (ThemeLightRadio.IsChecked == true)
                ThemeManager.SetTheme(false);
            else if (ThemeDarkRadio.IsChecked == true)
                ThemeManager.SetTheme(true);
            else
                ThemeManager.SetThemeAuto();
        }

        private void Language_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            if (LanguageCombo.SelectedItem is ComboBoxItem item)
            {
                string code = item.Tag?.ToString() ?? "en";
                App.SetLanguage(code);
            }
        }

        #endregion

        #region ================== SAVE & CANCEL ==================

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //--------------- Main Window ---------------
                Settings.FontName = FontNameText.Text;
                if (int.TryParse(FontSizeBox.Text, out int fs)) Settings.FontSize = fs;

                Settings.SwapUpDown = SwapUpDownCheck.IsChecked == true;
                Settings.SpeedShortMode = SpeedShortModeCheck.IsChecked == true;
                Settings.SeparateValueUnit = SeparateUnitCheck.IsChecked == true;
                Settings.ShowToolTip = ShowToolTipCheck.IsChecked == true;

                Settings.AlwaysOnTop = AlwaysOnTopCheck.IsChecked == true;
                Settings.LockWindowPos = LockWindowPosCheck.IsChecked == true;
                Settings.HideMainWindowFullscreen = HideMainWindowCheck.IsChecked == true;
                Settings.MousePenetrate = MousePenetrateCheck.IsChecked == true;

                Settings.DoubleClickAction = DoubleClickCombo.SelectedIndex;
                Settings.WindowOpacity = OpacitySlider.Value;

                Settings.UnitByte = UnitByteRadio.IsChecked == true;
                Settings.SpeedUnit = SpeedUnitCombo.SelectedIndex;

                if (BgTransparentCheck != null)
                    Settings.WindowBgTransparent = BgTransparentCheck.IsChecked == true;

                //--------------- Taskbar Window ---------------
                Settings.ShowTaskbarWindow = ShowTaskbarCheck.IsChecked == true;
                if (int.TryParse(TaskbarOffsetXBox.Text, out int ox)) Settings.TaskbarOffsetX = ox;
                if (int.TryParse(TaskbarOffsetYBox.Text, out int oy)) Settings.TaskbarOffsetY = oy;
                Settings.TaskbarSwapUpDown = TaskbarSwapUpDownCheck.IsChecked == true;
                Settings.TaskbarShortMode = TaskbarShortModeCheck.IsChecked == true;
                Settings.TaskbarShowArrows = TaskbarShowArrowsCheck.IsChecked == true;

                Settings.TaskbarOnLeft = TaskbarOnLeftCheck.IsChecked == true;
                Settings.TaskbarSeparateValueUnit = TaskbarSeparateUnitCheck.IsChecked == true;
                Settings.TaskbarDoubleClickAction = TaskbarDoubleClickCombo.SelectedIndex;
                Settings.DigitsNumber = DigitsNumberCombo.SelectedIndex == 0 ? 3 :
                                        DigitsNumberCombo.SelectedIndex == 2 ? 5 : 4;

                //--------------- General ---------------
                if (ThemeLightRadio.IsChecked == true) Settings.ThemeMode = 0;
                else if (ThemeDarkRadio.IsChecked == true) Settings.ThemeMode = 1;
                else Settings.ThemeMode = 2;

                Settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
                Settings.StartMinimized = MinimizedStartCheck.IsChecked == true;
                Settings.ShowTrayIcon = ShowTrayIconCheck.IsChecked == true;
                Settings.HardwareAcceleration = HardwareAccelCheck.IsChecked == true;

                if (int.TryParse(UpdateIntervalBox.Text, out int ui)) Settings.UpdateInterval = ui;
                Settings.AutoSelect = AutoSelectConnCheck.IsChecked == true;

                Settings.TrafficTipEnable = TrafficTipCheck.IsChecked == true;
                if (int.TryParse(TrafficTipValueBox.Text, out int tv))
                    Settings.TrafficTipValue = tv;
                Settings.TrafficTipUnit = TrafficTipUnitCombo.SelectedIndex;

                ThemeManager.SaveOpacity(Settings.WindowOpacity);

                SaveStartupSettings();
                App.SetTrayEnabled(Settings.ShowTrayIcon);

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Save error: {ex.Message}");
                MessageBox.Show($"Error saving settings: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Restore saved opacity before closing
            double savedOpacity = ThemeManager.GetSavedOpacity();
            ThemeManager.NotifyOpacityChanged(savedOpacity);
            this.Opacity = savedOpacity;

            this.DialogResult = false;
            this.Close();
        }

        #endregion

        #region ================== STARTUP REGISTRY ==================

        private void SaveStartupSettings()
        {
            try
            {
                using (var runKey = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (runKey == null) return;

                    string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath)) return;

                    if (Settings.StartWithWindows)
                        runKey.SetValue("TrafficMonitor", $"\"{exePath}\"");
                    else if (runKey.GetValue("TrafficMonitor") != null)
                        runKey.DeleteValue("TrafficMonitor", false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SaveStartupSettings error: {ex.Message}");
            }
        }

        #endregion

        #region ================== DRAG WINDOW ==================

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        #endregion

        #region ================== HELPERS ==================

        private WpfBrush ColorFromHex(string hex)
        {
            try
            {
                return (WpfBrush)new BrushConverter().ConvertFromString(hex);
            }
            catch
            {
                return new WpfBrush(Colors.White);
            }
        }

        #endregion
    }
}