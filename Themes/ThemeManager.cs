using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using TrafficMonitor.Services;

namespace TrafficMonitor.Themes
{
    public static class ThemeManager
    {
        #region ================== CONSTANTS ==================

        private const string REG_PATH = @"SOFTWARE\TrafficMonitor";
        private const int WM_SETTINGCHANGE = 0x001A;

        #endregion

        #region ================== EVENTS ==================

        public static event Action<bool> ThemeChanged;
        public static event Action<double> OpacityChanged;

        #endregion

        #region ================== PROPERTIES ==================

        public static bool IsDarkMode { get; private set; } = false;
        public static double CurrentOpacity { get; private set; } = 1.0;
        public static int ThemeMode { get; private set; } = 2;

        #endregion

        #region ================== PRIVATE FIELDS ==================

        private static HwndSource _messageWindow;

        #endregion

        #region ================== INITIALIZATION ==================

        public static void Initialize()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_PATH))
                {
                    if (key != null)
                    {
                        ThemeMode = Convert.ToInt32(key.GetValue("ThemeMode") ?? 2);

                        double opacityPercent = Convert.ToDouble(key.GetValue("Opacity") ?? 100);
                        if (opacityPercent <= 1.0 && opacityPercent > 0)
                            opacityPercent *= 100.0;
                        CurrentOpacity = Math.Max(0.3, Math.Min(1.0, opacityPercent / 100.0));
                    }
                }

                ApplyThemeByMode();
                CreateMessageListener();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ThemeManager.Initialize error: {ex.Message}");
                IsDarkMode = false;
                CurrentOpacity = 1.0;
                ThemeMode = 2;
            }

            ApplyThemeToApplication();
        }

        #endregion

        #region ================== MESSAGE LISTENER ==================

        private static void CreateMessageListener()
        {
            try
            {
                var parameters = new HwndSourceParameters("ThemeListener")
                {
                    Width = 0,
                    Height = 0,
                    PositionX = 0,
                    PositionY = 0,
                    ParentWindow = new IntPtr(-3),
                    WindowStyle = 0
                };
                _messageWindow = new HwndSource(parameters);
                _messageWindow.AddHook(WndProc);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CreateMessageListener error: {ex.Message}");
            }
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_SETTINGCHANGE)
            {
                try
                {
                    string param = lParam != IntPtr.Zero ? Marshal.PtrToStringUni(lParam) : null;
                    if (param == "ImmersiveColorSet" || string.IsNullOrEmpty(param))
                    {
                        if (ThemeMode == 2)
                        {
                            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                bool sysDark = IsWindowsDarkMode();
                                if (IsDarkMode != sysDark)
                                {
                                    IsDarkMode = sysDark;
                                    ApplyThemeToApplication();
                                    ThemeChanged?.Invoke(IsDarkMode);
                                }
                            }));
                        }
                    }
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        #endregion

        #region ================== THEME SETTERS ==================

        public static void SetTheme(bool isDark)
        {
            IsDarkMode = isDark;
            ThemeMode = isDark ? 1 : 0;
            ApplyThemeToApplication();
            SaveThemePreference();
            ThemeChanged?.Invoke(isDark);
        }

        public static void SetThemeAuto()
        {
            ThemeMode = 2;
            bool systemIsDark = IsWindowsDarkMode();
            IsDarkMode = systemIsDark;
            ApplyThemeToApplication();
            SaveThemePreference();
            ThemeChanged?.Invoke(IsDarkMode);
        }

        public static void SetThemeByMode(int mode)
        {
            switch (mode)
            {
                case 0: SetTheme(false); break;
                case 1: SetTheme(true); break;
                case 2: default: SetThemeAuto(); break;
            }
        }

        public static void ToggleTheme()
        {
            if (ThemeMode == 2) SetTheme(!IsWindowsDarkMode());
            else SetTheme(!IsDarkMode);
        }

        #endregion

        #region ================== OPACITY ==================

        public static void NotifyOpacityChanged(double opacity)
            => OpacityChanged?.Invoke(opacity);

        public static void SetOpacity(double opacity)
        {
            opacity = Math.Max(0.3, Math.Min(1.0, opacity));
            CurrentOpacity = opacity;
            SaveOpacityPreference();
            OpacityChanged?.Invoke(opacity);
        }

        public static void SaveOpacity(double opacity)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(REG_PATH);
                double valueToSave = opacity <= 1.0 ? opacity * 100.0 : opacity;
                key?.SetValue("Opacity", valueToSave);
            }
            catch { }
        }

        public static double GetSavedOpacity()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REG_PATH);
                if (key != null)
                {
                    double op = Convert.ToDouble(key.GetValue("Opacity") ?? 100);
                    if (op <= 1.0 && op > 0)
                        op *= 100.0;
                    return Math.Max(30, Math.Min(100, op)) / 100.0;
                }
            }
            catch { }
            return 1.0;
        }

        #endregion

        #region ================== PRIVATE HELPERS ==================

        private static void ApplyThemeByMode()
        {
            switch (ThemeMode)
            {
                case 0: IsDarkMode = false; break;
                case 1: IsDarkMode = true; break;
                case 2: default: IsDarkMode = IsWindowsDarkMode(); break;
            }
        }

        private static bool IsWindowsDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (key != null)
                {
                    object value = key.GetValue("AppsUseLightTheme");
                    if (value != null) return Convert.ToInt32(value) == 0;
                }
            }
            catch { }
            return false;
        }

        private static void ApplyThemeToApplication()
        {
            try
            {
                var resources = Application.Current.Resources;
                string suffix = IsDarkMode ? "Dark" : "";

                string[] keys = {
                    "WindowBg", "SidebarBg", "CardBg", "MainText", "SubText",
                    "Accent", "BorderBrush", "Border", "HoverBg", "TotalCardBg",
                    "Success", "Warning", "Error", "Info", "Purple", "Pink",
                    "Indigo", "Teal"
                };

                foreach (var keyName in keys)
                {
                    string sourceKey = keyName + suffix;
                    string dynamicKey = "Dynamic" + keyName;
                    if (resources.Contains(sourceKey) && resources[sourceKey] is SolidColorBrush brush)
                        resources[dynamicKey] = brush;
                }

                ToastManager.RefreshFlowDirection();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyThemeToApplication error: {ex.Message}");
            }
        }

        #endregion

        #region ================== PUBLIC METHODS ==================

        public static void ApplyThemeToWindow(Window window)
        {
            if (window == null) return;
            if (window is TaskbarWindow) return;
            try
            {
                window.Opacity = CurrentOpacity;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyThemeToWindow error: {ex.Message}");
            }
        }

        private static void SaveThemePreference()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(REG_PATH);
                key?.SetValue("ThemeMode", ThemeMode);
                key?.SetValue("Theme", IsDarkMode ? "Dark" : "Light");
            }
            catch { }
        }

        private static void SaveOpacityPreference() => SaveOpacity(CurrentOpacity);

        public static int GetThemeMode() => ThemeMode;
        public static bool GetIsDarkMode() => IsDarkMode;

        #endregion
    }
}