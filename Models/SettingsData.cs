using System;

namespace TrafficMonitor.Models
{
    public class SettingsData
    {
        #region ================== MAIN WINDOW SETTINGS ==================

        public int WindowLeft { get; set; } = 100;
        public int WindowTop { get; set; } = 100;
        public int WindowWidth { get; set; } = 290;
        public int WindowHeight { get; set; } = 180;
        public double WindowOpacity { get; set; } = 1.0;
        public bool AlwaysOnTop { get; set; } = true;
        public bool LockWindowPos { get; set; } = false;
        public bool HideMainWindow { get; set; } = false;
        public bool HideMainWindowFullscreen { get; set; } = false;
        public bool MousePenetrate { get; set; } = false;
        public int DoubleClickAction { get; set; } = 0;
        public bool WindowBgTransparent { get; set; } = false;

        #endregion

        #region ================== DISPLAY & FONT SETTINGS ==================

        public string FontName { get; set; } = "Segoe UI";
        public int FontSize { get; set; } = 10;
        public string TextColorHex { get; set; } = "#FFFFFF";
        public string BackColorHex { get; set; } = "#FF2D2D2D";
        public int ThemeMode { get; set; } = 2;

        public bool SwapUpDown { get; set; } = false;
        public bool SpeedShortMode { get; set; } = false;
        public bool SeparateValueUnit { get; set; } = false;
        public bool HideUnit { get; set; } = false;
        public int SpeedUnit { get; set; } = 0;
        public bool UnitByte { get; set; } = true;
        public bool ShowToolTip { get; set; } = true;

        #endregion

        #region ================== TASKBAR WINDOW SETTINGS ==================

        public bool ShowTaskbarWindow { get; set; } = false;
        public int TaskbarOffsetX { get; set; } = 0;
        public int TaskbarOffsetY { get; set; } = 0;
        public bool TaskbarSwapUpDown { get; set; } = false;
        public bool TaskbarShortMode { get; set; } = false;
        public bool TaskbarShowArrows { get; set; } = true;
        public bool TaskbarShowToolTip { get; set; } = true;
        public bool TaskbarSeparateValueUnit { get; set; } = true;
        public bool TaskbarValueRightAlign { get; set; } = false;
        public int TaskbarDoubleClickAction { get; set; } = 0;
        public bool TaskbarOnLeft { get; set; } = false;
        public int DigitsNumber { get; set; } = 4;
        public bool HorizontalArrange { get; set; } = true;
        public int ItemSpace { get; set; } = 4;
        public int VerticalMargin { get; set; } = 0;

        #endregion

        #region ================== RESOURCE GRAPH SETTINGS ==================

        public bool ShowNetSpeedGraph { get; set; } = false;
        public int NetSpeedGraphMaxValue { get; set; } = 512;
        public int NetSpeedGraphUnit { get; set; } = 0;
        public bool GraphPlotMode { get; set; } = false;

        #endregion

        #region ================== NETWORK SETTINGS ==================

        public bool AutoSelect { get; set; } = true;
        public string SelectedConnection { get; set; } = "";
        public int UpdateInterval { get; set; } = 1000;
        public bool ShowAllConnections { get; set; } = false;

        #endregion

        #region ================== GENERAL SETTINGS ==================

        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool ShowTrayIcon { get; set; } = true;
        public bool HardwareAcceleration { get; set; } = true;
        public bool CheckUpdateAtStart { get; set; } = true;

        #endregion

        #region ================== NOTIFICATION SETTINGS ==================

        public bool TrafficTipEnable { get; set; } = false;
        public int TrafficTipValue { get; set; } = 200;
        public int TrafficTipUnit { get; set; } = 0;

        #endregion

        #region ================== HELPER METHODS ==================

        public SettingsData Clone()
        {
            return (SettingsData)this.MemberwiseClone();
        }

        public void ResetToDefaults()
        {
            var defaults = new SettingsData();
            foreach (var prop in typeof(SettingsData).GetProperties())
            {
                if (prop.CanWrite)
                {
                    prop.SetValue(this, prop.GetValue(defaults));
                }
            }
        }

        #endregion
    }
}