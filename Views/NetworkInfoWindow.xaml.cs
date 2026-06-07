using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using TrafficMonitor.Services;
using TrafficMonitor.Themes;

namespace TrafficMonitor.Views
{
    public partial class NetworkInfoWindow : Window
    {
        #region ================== CONSTRUCTOR ==================

        public NetworkInfoWindow(IEnumerable<ConnectionInfo> connections)
        {
            InitializeComponent();

            this.Loaded += (s, e) => ThemeManager.ApplyThemeToWindow(this);
            ThemeManager.ThemeChanged += OnThemeChanged;
            this.Closed += (s, e) => ThemeManager.ThemeChanged -= OnThemeChanged;

            ConnectionsList.ItemsSource = connections;
        }

        #endregion

        #region ================== THEME HANDLING ==================

        private void OnThemeChanged(bool isDark)
        {
            Dispatcher.Invoke(() => ThemeManager.ApplyThemeToWindow(this));
        }

        #endregion

        #region ================== WINDOW EVENTS ==================

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => this.Close();

        #endregion
    }
}