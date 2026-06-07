using System;
using System.Diagnostics;
using Microsoft.Win32;
using TrafficMonitor.Models;

namespace TrafficMonitor.Services
{
    public class RegistrySettingsService
    {
        #region ================== CONSTANTS ==================

        private const string REG_PATH = @"SOFTWARE\TrafficMonitor";

        #endregion

        #region ================== LOAD ==================

        public SettingsData Load()
        {
            var s = new SettingsData();
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_PATH))
                {
                    if (key == null) return s;

                    //--------------- Main Window ---------------
                    s.WindowLeft = GetInt(key, "WindowLeft", s.WindowLeft);
                    s.WindowTop = GetInt(key, "WindowTop", s.WindowTop);
                    s.WindowWidth = GetInt(key, "WindowWidth", s.WindowWidth);
                    s.WindowHeight = GetInt(key, "WindowHeight", s.WindowHeight);

                    //--------------- Font / Color ---------------
                    s.FontName = GetString(key, "FontName", s.FontName);
                    s.FontSize = GetInt(key, "FontSize", s.FontSize);
                    s.TextColorHex = GetString(key, "TextColorHex", s.TextColorHex);
                    s.BackColorHex = GetString(key, "BackColorHex", s.BackColorHex);

                    //--------------- Display ---------------
                    s.SwapUpDown = GetBool(key, "SwapUpDown", s.SwapUpDown);
                    s.SpeedShortMode = GetBool(key, "SpeedShortMode", s.SpeedShortMode);
                    s.SeparateValueUnit = GetBool(key, "SeparateValueUnit", s.SeparateValueUnit);
                    s.ShowToolTip = GetBool(key, "ShowToolTip", s.ShowToolTip);
                    s.HideUnit = GetBool(key, "HideUnit", s.HideUnit);

                    //--------------- Speed Unit ---------------
                    s.UnitByte = GetBool(key, "UnitByte", s.UnitByte);
                    s.SpeedUnit = GetInt(key, "SpeedUnit", s.SpeedUnit);

                    //--------------- Window Behavior ---------------
                    s.AlwaysOnTop = GetBool(key, "AlwaysOnTop", s.AlwaysOnTop);
                    s.LockWindowPos = GetBool(key, "LockWindowPos", s.LockWindowPos);
                    s.HideMainWindow = GetBool(key, "HideMainWindow", s.HideMainWindow);
                    s.HideMainWindowFullscreen = GetBool(key, "HideMainWindowFullscreen", s.HideMainWindowFullscreen);
                    s.MousePenetrate = GetBool(key, "MousePenetrate", s.MousePenetrate);

                    s.DoubleClickAction = GetInt(key, "DoubleClickAction", s.DoubleClickAction);
                    s.WindowOpacity = GetDouble(key, "WindowOpacity", s.WindowOpacity);

                    //--------------- Taskbar Window ---------------
                    s.ShowTaskbarWindow = GetBool(key, "ShowTaskbarWindow", s.ShowTaskbarWindow);
                    s.TaskbarOffsetX = GetInt(key, "TaskbarOffsetX", s.TaskbarOffsetX);
                    s.TaskbarOffsetY = GetInt(key, "TaskbarOffsetY", s.TaskbarOffsetY);
                    s.TaskbarSwapUpDown = GetBool(key, "TaskbarSwapUpDown", s.TaskbarSwapUpDown);
                    s.TaskbarShortMode = GetBool(key, "TaskbarShortMode", s.TaskbarShortMode);
                    s.TaskbarShowArrows = GetBool(key, "TaskbarShowArrows", s.TaskbarShowArrows);
                    s.TaskbarShowToolTip = GetBool(key, "TaskbarShowToolTip", s.TaskbarShowToolTip);
                    s.TaskbarSeparateValueUnit = GetBool(key, "TaskbarSeparateValueUnit", s.TaskbarSeparateValueUnit);
                    s.TaskbarValueRightAlign = GetBool(key, "TaskbarValueRightAlign", s.TaskbarValueRightAlign);
                    s.TaskbarDoubleClickAction = GetInt(key, "TaskbarDoubleClickAction", s.TaskbarDoubleClickAction);
                    s.TaskbarOnLeft = GetBool(key, "TaskbarOnLeft", s.TaskbarOnLeft);
                    s.DigitsNumber = GetInt(key, "DigitsNumber", s.DigitsNumber);
                    s.HorizontalArrange = GetBool(key, "HorizontalArrange", s.HorizontalArrange);
                    s.ItemSpace = GetInt(key, "ItemSpace", s.ItemSpace);
                    s.VerticalMargin = GetInt(key, "VerticalMargin", s.VerticalMargin);

                    //--------------- Graph ---------------
                    s.ShowNetSpeedGraph = GetBool(key, "ShowNetSpeedGraph", s.ShowNetSpeedGraph);
                    s.NetSpeedGraphMaxValue = GetInt(key, "NetSpeedGraphMaxValue", s.NetSpeedGraphMaxValue);
                    s.NetSpeedGraphUnit = GetInt(key, "NetSpeedGraphUnit", s.NetSpeedGraphUnit);
                    s.GraphPlotMode = GetBool(key, "GraphPlotMode", s.GraphPlotMode);

                    //--------------- General ---------------
                    s.ThemeMode = GetInt(key, "ThemeMode", s.ThemeMode);
                    s.StartWithWindows = GetBool(key, "StartWithWindows", s.StartWithWindows);
                    s.StartMinimized = GetBool(key, "StartMinimized", s.StartMinimized);
                    s.ShowTrayIcon = GetBool(key, "ShowTrayIcon", s.ShowTrayIcon);
                    s.HardwareAcceleration = GetBool(key, "HardwareAccel", s.HardwareAcceleration);
                    s.CheckUpdateAtStart = GetBool(key, "CheckUpdateAtStart", s.CheckUpdateAtStart);

                    //--------------- Network ---------------
                    s.UpdateInterval = GetInt(key, "UpdateInterval", s.UpdateInterval);
                    s.AutoSelect = GetBool(key, "AutoSelect", s.AutoSelect);
                    s.SelectedConnection = GetString(key, "SelectedConnection", s.SelectedConnection);
                    s.ShowAllConnections = GetBool(key, "ShowAllConnections", s.ShowAllConnections);

                    //--------------- Notifications ---------------
                    s.TrafficTipEnable = GetBool(key, "TrafficTipEnable", s.TrafficTipEnable);
                    s.TrafficTipValue = GetInt(key, "TrafficTipValue", s.TrafficTipValue);
                    s.TrafficTipUnit = GetInt(key, "TrafficTipUnit", s.TrafficTipUnit);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Settings Load error: {ex.Message}");
            }
            return s;
        }

        #endregion

        #region ================== SAVE ==================

        public void Save(SettingsData s)
        {
            if (s == null) return;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(REG_PATH))
                {
                    if (key == null) return;

                    //--------------- Main Window ---------------
                    key.SetValue("WindowLeft", s.WindowLeft);
                    key.SetValue("WindowTop", s.WindowTop);
                    key.SetValue("WindowWidth", s.WindowWidth);
                    key.SetValue("WindowHeight", s.WindowHeight);

                    //--------------- Font / Color ---------------
                    key.SetValue("FontName", s.FontName ?? "Segoe UI");
                    key.SetValue("FontSize", s.FontSize);
                    key.SetValue("TextColorHex", s.TextColorHex ?? "#FFFFFF");
                    key.SetValue("BackColorHex", s.BackColorHex ?? "#FF2D2D2D");

                    //--------------- Display ---------------
                    key.SetValue("SwapUpDown", s.SwapUpDown ? 1 : 0);
                    key.SetValue("SpeedShortMode", s.SpeedShortMode ? 1 : 0);
                    key.SetValue("SeparateValueUnit", s.SeparateValueUnit ? 1 : 0);
                    key.SetValue("ShowToolTip", s.ShowToolTip ? 1 : 0);
                    key.SetValue("HideUnit", s.HideUnit ? 1 : 0);

                    //--------------- Speed Unit ---------------
                    key.SetValue("UnitByte", s.UnitByte ? 1 : 0);
                    key.SetValue("SpeedUnit", s.SpeedUnit);

                    //--------------- Window Behavior ---------------
                    key.SetValue("AlwaysOnTop", s.AlwaysOnTop ? 1 : 0);
                    key.SetValue("LockWindowPos", s.LockWindowPos ? 1 : 0);
                    key.SetValue("HideMainWindow", s.HideMainWindow ? 1 : 0);
                    key.SetValue("HideMainWindowFullscreen", s.HideMainWindowFullscreen ? 1 : 0);
                    key.SetValue("MousePenetrate", s.MousePenetrate ? 1 : 0);

                    key.SetValue("DoubleClickAction", s.DoubleClickAction);
                    key.SetValue("WindowOpacity",
                        s.WindowOpacity.ToString(System.Globalization.CultureInfo.InvariantCulture));

                    //--------------- Taskbar Window ---------------
                    key.SetValue("ShowTaskbarWindow", s.ShowTaskbarWindow ? 1 : 0);
                    key.SetValue("TaskbarOffsetX", s.TaskbarOffsetX);
                    key.SetValue("TaskbarOffsetY", s.TaskbarOffsetY);
                    key.SetValue("TaskbarSwapUpDown", s.TaskbarSwapUpDown ? 1 : 0);
                    key.SetValue("TaskbarShortMode", s.TaskbarShortMode ? 1 : 0);
                    key.SetValue("TaskbarShowArrows", s.TaskbarShowArrows ? 1 : 0);
                    key.SetValue("TaskbarShowToolTip", s.TaskbarShowToolTip ? 1 : 0);
                    key.SetValue("TaskbarSeparateValueUnit", s.TaskbarSeparateValueUnit ? 1 : 0);
                    key.SetValue("TaskbarValueRightAlign", s.TaskbarValueRightAlign ? 1 : 0);
                    key.SetValue("TaskbarDoubleClickAction", s.TaskbarDoubleClickAction);
                    key.SetValue("TaskbarOnLeft", s.TaskbarOnLeft ? 1 : 0);
                    key.SetValue("DigitsNumber", s.DigitsNumber);
                    key.SetValue("HorizontalArrange", s.HorizontalArrange ? 1 : 0);
                    key.SetValue("ItemSpace", s.ItemSpace);
                    key.SetValue("VerticalMargin", s.VerticalMargin);

                    //--------------- Graph ---------------
                    key.SetValue("ShowNetSpeedGraph", s.ShowNetSpeedGraph ? 1 : 0);
                    key.SetValue("NetSpeedGraphMaxValue", s.NetSpeedGraphMaxValue);
                    key.SetValue("NetSpeedGraphUnit", s.NetSpeedGraphUnit);
                    key.SetValue("GraphPlotMode", s.GraphPlotMode ? 1 : 0);

                    //--------------- General ---------------
                    key.SetValue("ThemeMode", s.ThemeMode);
                    key.SetValue("StartWithWindows", s.StartWithWindows ? 1 : 0);
                    key.SetValue("StartMinimized", s.StartMinimized ? 1 : 0);
                    key.SetValue("ShowTrayIcon", s.ShowTrayIcon ? 1 : 0);
                    key.SetValue("HardwareAccel", s.HardwareAcceleration ? 1 : 0);
                    key.SetValue("CheckUpdateAtStart", s.CheckUpdateAtStart ? 1 : 0);

                    //--------------- Network ---------------
                    key.SetValue("UpdateInterval", s.UpdateInterval);
                    key.SetValue("AutoSelect", s.AutoSelect ? 1 : 0);
                    key.SetValue("SelectedConnection", s.SelectedConnection ?? "");
                    key.SetValue("ShowAllConnections", s.ShowAllConnections ? 1 : 0);

                    //--------------- Notifications ---------------
                    key.SetValue("TrafficTipEnable", s.TrafficTipEnable ? 1 : 0);
                    key.SetValue("TrafficTipValue", s.TrafficTipValue);
                    key.SetValue("TrafficTipUnit", s.TrafficTipUnit);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Settings Save error: {ex.Message}");
            }
        }

        #endregion

        #region ================== HELPERS ==================

        private int GetInt(RegistryKey key, string name, int def)
        {
            try { return Convert.ToInt32(key.GetValue(name, def)); }
            catch { return def; }
        }

        private bool GetBool(RegistryKey key, string name, bool def)
        {
            try { return Convert.ToInt32(key.GetValue(name, def ? 1 : 0)) != 0; }
            catch { return def; }
        }

        private string GetString(RegistryKey key, string name, string def)
        {
            try { return key.GetValue(name, def)?.ToString() ?? def; }
            catch { return def; }
        }

        private double GetDouble(RegistryKey key, string name, double def)
        {
            try
            {
                var val = key.GetValue(name, def.ToString(System.Globalization.CultureInfo.InvariantCulture))?.ToString();
                if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double result))
                    return result;
            }
            catch { }
            return def;
        }

        #endregion
    }
}