using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using TrafficMonitor.Themes;

namespace TrafficMonitor.Views
{
    public partial class AboutWindow : Window
    {
        #region ================== PRIVATE FIELDS ==================

        private bool _isDarkMode = false;


        #endregion

        #region ================== CONSTRUCTOR ==================

        public AboutWindow()
        {
            InitializeComponent();

            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = System.Windows.Media.Brushes.Transparent;

            ThemeManager.ThemeChanged += OnThemeManagerChanged;
            ThemeManager.OpacityChanged += OnOpacityManagerChanged;

            this.Loaded += OnPageLoaded;
            this.Closed += OnPageClosed;

            this.ShowInTaskbar = false;
            this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }

        #endregion

        #region ================== THEME & OPACITY ==================

        private void ApplySavedOpacity()
        {
            try
            {
                double opacity = ThemeManager.GetSavedOpacity();
                this.Opacity = opacity;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error applying opacity: {ex.Message}");
            }
        }

        private void OnThemeManagerChanged(bool isDark)
        {
            if (!this.IsLoaded && this.Visibility != Visibility.Visible)
                return;

            Dispatcher.Invoke(() =>
            {
                if (this.IsLoaded || this.Visibility == Visibility.Visible)
                {
                    _isDarkMode = isDark;
                    ApplyTheme();
                }
            });
        }

        private void OnOpacityManagerChanged(double opacity)
        {
            if (!this.IsLoaded && this.Visibility != Visibility.Visible)
                return;

            Dispatcher.Invoke(() =>
            {
                if (this.IsLoaded || this.Visibility == Visibility.Visible)
                {
                    this.Opacity = opacity;
                }
            });
        }

        #endregion

        #region ================== PAGE EVENTS ==================

        private void OnPageLoaded(object sender, EventArgs e)
        {
            try
            {
                _isDarkMode = ThemeManager.IsDarkMode;
                ApplyTheme();
                ApplySavedOpacity();

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"About load error: {ex.Message}");
            }
        }

        private void OnPageClosed(object sender, EventArgs e)
        {

            try
            {

                ThemeManager.ThemeChanged -= OnThemeManagerChanged;
                ThemeManager.OpacityChanged -= OnOpacityManagerChanged;
                this.Loaded -= OnPageLoaded;
                this.Closed -= OnPageClosed;
            }
            catch { }
        }

        #endregion


        #region ================== APPLY THEME ==================

        private void ApplyTheme()
        {
            try
            {
                ThemeManager.ApplyThemeToWindow(this);
                if (btnClose != null && TryFindResource("DynamicError") is System.Windows.Media.Brush brush)
                    btnClose.Foreground = brush;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ApplyTheme error in About: {ex.Message}");
            }
        }

        #endregion

        #region ================== EVENT HANDLERS ==================

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                this.DragMove();
        }

        private void btnWebsite_Click(object sender, RoutedEventArgs e)
        {
            OpenLink("https://github.com/Diagoo1");
        }

        private void btnPayPal_Click(object sender, RoutedEventArgs e)
        {
            OpenLink("https://paypal.me/Diagoo1");
        }

        private void btnEmail_Click(object sender, RoutedEventArgs e)
        {
            OpenLink("mailto:tarek.sadek44@gmail.com");
        }

        private void OpenLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    App.T("About_LinkError", "Could not open link: {0}", ex.Message),
                    App.T("Common_Error", "Error"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion

        #region ================== PUBLIC METHODS ==================

        public void ToggleTheme()
        {
            ThemeManager.ToggleTheme();
        }

        public void SetDarkTheme()
        {
            ThemeManager.SetTheme(true);
        }

        public void SetLightTheme()
        {
            ThemeManager.SetTheme(false);
        }

        public bool IsDarkMode => ThemeManager.IsDarkMode;

        #endregion
    }
}