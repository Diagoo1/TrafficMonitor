using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace TrafficMonitor.Services
{
    public enum ToastType { Success, Warning, Error, Info }

    public static class ToastManager
    {
        #region ================== FIELDS ==================

        private static Window _toastHost;
        private static StackPanel _toastContainer;
        private static readonly List<Border> _activeToasts = new List<Border>();
        private const int MAX_TOASTS = 5;

        #endregion

        #region ================== WIN32 API ==================

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_APPWINDOW = 0x00040000;

        private static void HideFromAltTab(Window window)
        {
            try
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                exStyle = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
                SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
            }
            catch { }
        }

        #endregion

        #region ================== PUBLIC API ==================

        //--------------- Localized Messages ---------------
        public static void Success(string messageKey, params object[] args)
            => Show(messageKey, ToastType.Success, 3000, args);

        public static void Warning(string messageKey, params object[] args)
            => Show(messageKey, ToastType.Warning, 3500, args);

        public static void Error(string messageKey, params object[] args)
            => Show(messageKey, ToastType.Error, 4000, args);

        public static void Info(string messageKey, params object[] args)
            => Show(messageKey, ToastType.Info, 3000, args);

        //--------------- Direct Messages ---------------
        public static void SuccessDirect(string message, int durationMs = 3000)
            => ShowDirect(message, ToastType.Success, durationMs);

        public static void WarningDirect(string message, int durationMs = 3500)
            => ShowDirect(message, ToastType.Warning, durationMs);

        public static void ErrorDirect(string message, int durationMs = 4000)
            => ShowDirect(message, ToastType.Error, durationMs);

        public static void InfoDirect(string message, int durationMs = 3000)
            => ShowDirect(message, ToastType.Info, durationMs);

        #endregion

        #region ================== CORE LOGIC ==================

        private static void Show(string messageKey, ToastType type, int durationMs, params object[] args)
        {
            string localizedMessage = GetLocalizedString(messageKey, messageKey);

            if (args != null && args.Length > 0)
            {
                try
                {
                    localizedMessage = string.Format(localizedMessage, args);
                }
                catch { }
            }

            ShowDirect(localizedMessage, type, durationMs);
        }

        private static void ShowDirect(string message, ToastType type, int durationMs)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                EnsureHost();
                var toast = BuildToast(message, type);
                _toastContainer.Children.Insert(0, toast);
                _activeToasts.Insert(0, toast);

                while (_activeToasts.Count > MAX_TOASTS)
                {
                    var oldest = _activeToasts.Last();
                    _activeToasts.Remove(oldest);
                    _toastContainer.Children.Remove(oldest);
                }

                AnimateIn(toast);

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
                timer.Tick += (s, e) => { timer.Stop(); AnimateOut(toast); };
                timer.Start();
            });
        }

        private static string GetLocalizedString(string key, string fallback)
        {
            try
            {
                var resource = Application.Current.TryFindResource(key);
                return resource?.ToString() ?? fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static void EnsureHost()
        {
            bool isRtl = IsRtl();

            if (_toastHost != null && _toastHost.IsVisible)
            {
                var needed = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
                if (_toastHost.FlowDirection != needed)
                {
                    _toastHost.FlowDirection = needed;
                    _toastContainer.HorizontalAlignment = isRtl
                        ? HorizontalAlignment.Left
                        : HorizontalAlignment.Right;
                }
                return;
            }

            _toastContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = isRtl ? HorizontalAlignment.Left : HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20),
                Background = Brushes.Transparent
            };

            _toastHost = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize,
                IsHitTestVisible = true,
                Focusable = false,
                ShowActivated = false,
                Width = SystemParameters.WorkArea.Width,
                Height = SystemParameters.WorkArea.Height,
                Left = SystemParameters.WorkArea.Left,
                Top = SystemParameters.WorkArea.Top,
                Content = _toastContainer,
                FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
            };

            _toastHost.Closed += (s, e) =>
            {
                _toastHost = null;
                _activeToasts.Clear();
            };

            _toastHost.SourceInitialized += (s, e) => HideFromAltTab(_toastHost);
            _toastHost.Show();
        }

        public static void RefreshFlowDirection()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (_toastHost == null) return;

                var oldHost = _toastHost;
                _toastHost = null;
                _toastContainer = null;
                _activeToasts.Clear();

                try
                {
                    oldHost.Content = null;
                    oldHost.Close();
                }
                catch { }
            });
        }

        #endregion

        #region ================== UI BUILDER ==================

        private static Border BuildToast(string message, ToastType type)
        {
            var (accentColor, icon) = GetTypeStyle(type);
            bool isRtl = IsRtl();

            var outer = new Border
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 10),
                MinWidth = 280,
                MaxWidth = 380,
                Padding = new Thickness(0),
                IsHitTestVisible = true,
                Cursor = System.Windows.Input.Cursors.Hand,
                Effect = new DropShadowEffect
                {
                    BlurRadius = 20,
                    ShadowDepth = 3,
                    Opacity = 0.35,
                    Color = Colors.Black
                },
                RenderTransform = new TranslateTransform(400, 0),
                Opacity = 0
            };

            outer.SetResourceReference(Border.BackgroundProperty, "DynamicCardBg");
            outer.SetResourceReference(Border.BorderBrushProperty, "DynamicBorderBrush");

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            //--------------- Side Bar ---------------
            var sideBar = new Border
            {
                Background = new SolidColorBrush(accentColor),
                CornerRadius = new CornerRadius(12, 0, 0, 12),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 6,
                    ShadowDepth = 0,
                    Opacity = 0.4,
                    Color = accentColor
                }
            };
            Grid.SetColumn(sideBar, 0);

            //--------------- Icon Box ---------------
            var iconBox = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(28, accentColor.R, accentColor.G, accentColor.B)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 12, 10, 12),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 8,
                    ShadowDepth = 0,
                    Opacity = 0.2,
                    Color = accentColor
                },
                Child = new TextBlock
                {
                    Text = icon,
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 16,
                    Foreground = new SolidColorBrush(accentColor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight
                }
            };
            Grid.SetColumn(iconBox, 1);

            //--------------- Message Text ---------------
            var text = new TextBlock
            {
                Text = message,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 14, 10, 14),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                TextAlignment = isRtl ? TextAlignment.Right : TextAlignment.Left
            };
            text.SetResourceReference(TextBlock.ForegroundProperty, "DynamicMainText");
            Grid.SetColumn(text, 2);

            //--------------- Close Button ---------------
            var closeTb = new TextBlock
            {
                Text = "\uE711",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 11,
                FlowDirection = isRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
                Opacity = 0.4
            };
            closeTb.SetResourceReference(TextBlock.ForegroundProperty, "DynamicSubText");

            var closeBtn = new Button
            {
                Content = closeTb,
                Width = 30,
                Height = 30,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 14, 0),
                Template = BuildCloseButtonTemplate(),
                ToolTip = GetLocalizedString("Toast_Close", "Close")
            };
            closeBtn.SetResourceReference(Button.BackgroundProperty, "Transparent");

            closeBtn.MouseEnter += (s, e) => closeTb.Opacity = 0.9;
            closeBtn.MouseLeave += (s, e) => closeTb.Opacity = 0.4;
            closeBtn.Click += (s, e) => { e.Handled = true; AnimateOut(outer); };
            Grid.SetColumn(closeBtn, 3);

            grid.Children.Add(sideBar);
            grid.Children.Add(iconBox);
            grid.Children.Add(text);
            grid.Children.Add(closeBtn);

            outer.Child = grid;

            outer.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is Button) return;
                AnimateOut(outer);
            };

            return outer;
        }

        private static ControlTemplate BuildCloseButtonTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            return template;
        }

        private static (Color color, string icon) GetTypeStyle(ToastType type)
        {
            switch (type)
            {
                case ToastType.Success:
                    var successBrush = Application.Current.Resources["DynamicSuccess"] as SolidColorBrush;
                    var successColor = successBrush?.Color ?? Color.FromRgb(34, 197, 94);
                    return (successColor, "\uE73E");

                case ToastType.Warning:
                    var warningBrush = Application.Current.Resources["DynamicWarning"] as SolidColorBrush;
                    var warningColor = warningBrush?.Color ?? Color.FromRgb(249, 115, 22);
                    return (warningColor, "\uE7BA");

                case ToastType.Error:
                    var errorBrush = Application.Current.Resources["DynamicError"] as SolidColorBrush;
                    var errorColor = errorBrush?.Color ?? Color.FromRgb(239, 68, 68);
                    return (errorColor, "\uEA39");

                case ToastType.Info:
                default:
                    var infoBrush = Application.Current.Resources["DynamicInfo"] as SolidColorBrush;
                    var infoColor = infoBrush?.Color ?? Color.FromRgb(14, 165, 233);
                    return (infoColor, "\uE946");
            }
        }

        #endregion

        #region ================== ANIMATIONS ==================

        private static void AnimateIn(Border toast)
        {
            double fromX = IsRtl() ? -400 : 400;
            var transform = (TranslateTransform)toast.RenderTransform;
            transform.X = fromX;

            var slide = new DoubleAnimation
            {
                From = fromX,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300)
            };

            transform.BeginAnimation(TranslateTransform.XProperty, slide);
            toast.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private static void AnimateOut(Border toast)
        {
            if (toast == null || !_activeToasts.Contains(toast)) return;
            _activeToasts.Remove(toast);

            double toX = IsRtl() ? -400 : 400;
            var transform = (TranslateTransform)toast.RenderTransform;

            var slide = new DoubleAnimation
            {
                To = toX,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fade = new DoubleAnimation
            {
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250)
            };
            fade.Completed += (s, e) =>
            {
                if (_toastContainer != null && _toastContainer.Children.Contains(toast))
                    _toastContainer.Children.Remove(toast);
            };

            transform.BeginAnimation(TranslateTransform.XProperty, slide);
            toast.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        #endregion

        #region ================== HELPERS ==================

        private static bool IsRtl()
            => Application.Current.Resources["ApplicationFlowDirection"] is FlowDirection fd
               && fd == FlowDirection.RightToLeft;

        #endregion
    }
}