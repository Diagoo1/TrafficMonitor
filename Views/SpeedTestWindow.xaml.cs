using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using TrafficMonitor.Services;
using TrafficMonitor.Themes;

namespace TrafficMonitor.Views
{
    public class RatingFactor
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public string Status { get; set; }
        public double Weight { get; set; }
        public bool IsBottleneck { get; set; }
    }

    public class ActivityRating
    {
        public string Title { get; set; }
        public string Icon { get; set; }
        public string ShortLabel { get; set; }
        public int Score { get; set; }
        public Color Color { get; set; }
        public string Summary { get; set; }
        public List<RatingFactor> Factors { get; set; }
        public string Recommendation { get; set; }
    }

    public partial class SpeedTestWindow : Window
    {
        #region ================== PRIVATE FIELDS ==================

        private SpeedTestService _service;
        private CancellationTokenSource _cts;
        private bool _isTesting = false;
        private double _currentDisplayValue = 0;

        private static readonly Color DownloadColor = Color.FromRgb(34, 197, 94);
        private static readonly Color UploadColor = Color.FromRgb(14, 165, 233);
        private static readonly Color PingColor = Color.FromRgb(139, 92, 246);

        private SpeedTestPhase _currentPhase = SpeedTestPhase.Ping;
        private double _maxDownloadShown = 0;
        private double _maxUploadShown = 0;

        private bool _isTransitioning = false;
        private SpeedTestPhase _lastRenderedPhase = SpeedTestPhase.Ping;
        private System.Windows.Threading.DispatcherTimer _dotsTimer;
        private int _dotsCount = 0;
        private string _baseTransitionMessage = "";

        private ActivityRating _gamingRating, _streamingRating, _videoCallRating, _browsingRating;

        #endregion

        #region ================== GAUGE CONSTANTS ==================

        private const double GaugeCenterX = 140;
        private const double GaugeCenterY = 140;
        private const double GaugeRadius = 95;
        private const double GaugeTicksRadius = 115;
        private const double StartAngle = 225;
        private const double TotalSweep = 270;

        #endregion

        #region ================== DEPENDENCY PROPERTIES ==================

        public static readonly DependencyProperty GaugeProgressProperty =
            DependencyProperty.Register(nameof(GaugeProgress), typeof(double), typeof(SpeedTestWindow),
                new PropertyMetadata(0.0, OnGaugeProgressChanged));

        public static readonly DependencyProperty DisplayValueProperty =
            DependencyProperty.Register(nameof(DisplayValue), typeof(double), typeof(SpeedTestWindow),
                new PropertyMetadata(0.0, OnDisplayValueChanged));

        public double GaugeProgress
        {
            get => (double)GetValue(GaugeProgressProperty);
            set => SetValue(GaugeProgressProperty, value);
        }

        public double DisplayValue
        {
            get => (double)GetValue(DisplayValueProperty);
            set => SetValue(DisplayValueProperty, value);
        }

        #endregion

        #region ================== CONSTRUCTOR & INIT ==================

        public SpeedTestWindow()
        {
            InitializeComponent();
            ThemeManager.ApplyThemeToWindow(this);
            ThemeManager.ThemeChanged += OnThemeChanged;
            this.Loaded += OnWindowLoaded;
            this.Closed += OnWindowClosed;
        }

        private async void OnWindowClosed(object sender, EventArgs e)
        {
            ThemeManager.ThemeChanged -= OnThemeChanged;
            StopDotsAnimation();
            try
            {
                _cts?.Cancel();
                await Task.Delay(100);
            }
            catch { }
            finally
            {
                _cts?.Dispose();
                _service?.Dispose();
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                GenerateGaugeTicks();
                SetGaugeStartPoint();
                UpdateGaugeVisuals(0);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnThemeChanged(bool isDark)
            => Dispatcher.Invoke(() => ThemeManager.ApplyThemeToWindow(this));

        #endregion

        #region ================== GAUGE GEOMETRY ==================

        private Point AngleToPoint(double angleDegrees)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            double x = GaugeCenterX + GaugeRadius * Math.Sin(rad);
            double y = GaugeCenterY - GaugeRadius * Math.Cos(rad);
            return new Point(x, y);
        }

        private Point AngleToPoint(double angleDegrees, double radius)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            double x = GaugeCenterX + radius * Math.Sin(rad);
            double y = GaugeCenterY - radius * Math.Cos(rad);
            return new Point(x, y);
        }

        private void SetGaugeStartPoint()
        {
            var startPoint = AngleToPoint(StartAngle);
            GaugeArcFigure.StartPoint = startPoint;
            GaugeTrackFigure.StartPoint = startPoint;

            var trackEnd = AngleToPoint(StartAngle + TotalSweep);
            GaugeTrackSegment.Point = trackEnd;
            GaugeTrackSegment.IsLargeArc = TotalSweep > 180;
            GaugeTrackSegment.SweepDirection = SweepDirection.Clockwise;

            GaugeArcSegment.Point = startPoint;
            GaugeArcSegment.SweepDirection = SweepDirection.Clockwise;
        }

        private void UpdateGaugeVisuals(double mbps)
        {
            if (GaugeArcSegment == null) return;

            double percent = SpeedToPercent(mbps);
            double currentAngle = StartAngle + (percent / 100.0 * TotalSweep);

            var endPoint = AngleToPoint(currentAngle);
            GaugeArcSegment.Point = endPoint;
            GaugeArcSegment.IsLargeArc = (currentAngle - StartAngle) > 180;
            GaugeArcSegment.SweepDirection = SweepDirection.Clockwise;

            if (NeedleTransform != null)
            {
                NeedleTransform.Angle = -135 + (percent / 100.0 * 270);
            }
        }

        private double SpeedToPercent(double mbps)
        {
            if (mbps <= 0) return 0;
            if (mbps >= 1000) return 100;
            return Math.Min(100, Math.Max(0, Math.Log10(mbps + 1) / 3.0 * 100));
        }

        #endregion

        #region ================== DEPENDENCY PROPERTY HANDLERS ==================

        private static void OnGaugeProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((SpeedTestWindow)d).UpdateGaugeVisuals((double)e.NewValue);
        }

        private static void OnDisplayValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var win = (SpeedTestWindow)d;
            double v = (double)e.NewValue;
            win._currentDisplayValue = v;
            if (win.GaugeValueText != null)
                win.GaugeValueText.Text = v < 10 ? v.ToString("F1") : v.ToString("F0");
        }

        #endregion

        #region ================== GAUGE TICKS ==================

        private void GenerateGaugeTicks()
        {
            if (GaugeTicksCanvas == null) return;
            GaugeTicksCanvas.Children.Clear();

            var speeds = new[] { 0, 1, 5, 10, 25, 50, 100, 250, 500, 1000 };

            foreach (var speed in speeds)
            {
                double percent = SpeedToPercent(speed);
                double angle = StartAngle + (percent / 100.0 * TotalSweep);
                var pt = AngleToPoint(angle, GaugeTicksRadius);

                var tb = new TextBlock
                {
                    Text = speed >= 1000 ? "1G" : speed.ToString(),
                    Foreground = (Brush)(TryFindResource("DynamicSubText") ?? Brushes.Gray),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

                Canvas.SetLeft(tb, pt.X - tb.DesiredSize.Width / 2);
                Canvas.SetTop(tb, pt.Y - tb.DesiredSize.Height / 2);
                GaugeTicksCanvas.Children.Add(tb);
            }
        }

        #endregion

        #region ================== PHASE VISUALS ==================

        private void UpdatePhaseVisuals(SpeedTestPhase phase)
        {
            Color color;
            Brush gradient;

            switch (phase)
            {
                case SpeedTestPhase.Download:
                    color = DownloadColor;
                    gradient = (Brush)TryFindResource("SuccessGradient") ?? new SolidColorBrush(color);
                    break;
                case SpeedTestPhase.Upload:
                    color = UploadColor;
                    gradient = (Brush)TryFindResource("AccentGradient") ?? new SolidColorBrush(color);
                    break;
                default:
                    color = PingColor;
                    gradient = new SolidColorBrush(color);
                    break;
            }

            GaugeArc.Stroke = gradient;

            if (GaugeNeedle != null)
                GaugeNeedle.Fill = new SolidColorBrush(color);

            if (GaugeNeedle?.Effect is DropShadowEffect ds)
                ds.Color = color;

            if (GaugeArc?.Effect is DropShadowEffect ds2)
                ds2.Color = color;

            if (GaugePhaseBadge != null && GaugeLabelText != null)
            {
                GaugePhaseBadge.Background = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B));
                GaugeLabelText.Foreground = new SolidColorBrush(color);
            }
        }

        #endregion

        #region ================== DOTS ANIMATION ==================

        private void StartDotsAnimation(string baseMessage)
        {
            _baseTransitionMessage = baseMessage;
            _dotsCount = 0;

            _dotsTimer?.Stop();
            _dotsTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _dotsTimer.Tick += (s, e) =>
            {
                _dotsCount = (_dotsCount + 1) % 4;
                string dots = new string('.', _dotsCount);
                StatusText.Text = $"{_baseTransitionMessage}{dots}";
            };
            _dotsTimer.Start();
        }

        private void StopDotsAnimation()
        {
            _dotsTimer?.Stop();
            _dotsTimer = null;
        }

        #endregion

        #region ================== PHASE TRANSITION ==================

        private async Task PerformPhaseTransitionAsync(SpeedTestPhase nextPhase, string transitionMessageKey)
        {
            _isTransitioning = true;

            try
            {
                double currentGauge = (double)this.GetValue(GaugeProgressProperty);
                double currentDisplay = (double)this.GetValue(DisplayValueProperty);

                this.BeginAnimation(GaugeProgressProperty, null);
                this.BeginAnimation(DisplayValueProperty, null);
                this.SetValue(GaugeProgressProperty, currentGauge);
                this.SetValue(DisplayValueProperty, currentDisplay);

                StatusText.Text = App.T(transitionMessageKey, transitionMessageKey);
                GaugeLabelText.Text = App.T("SpeedTest_Preparing", "PREPARING");

                var arcReset = new DoubleAnimation
                {
                    From = currentGauge,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(900),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                    FillBehavior = FillBehavior.HoldEnd
                };

                var textReset = new DoubleAnimation
                {
                    From = currentDisplay,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(900),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
                    FillBehavior = FillBehavior.HoldEnd
                };

                this.BeginAnimation(GaugeProgressProperty, arcReset);
                this.BeginAnimation(DisplayValueProperty, textReset);

                var fadePulse = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.4,
                    Duration = TimeSpan.FromMilliseconds(600),
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(2),
                    EasingFunction = new SineEase()
                };
                GaugePhaseBadge?.BeginAnimation(OpacityProperty, fadePulse);

                await Task.Delay(900);

                this.BeginAnimation(GaugeProgressProperty, null);
                this.BeginAnimation(DisplayValueProperty, null);
                this.SetValue(GaugeProgressProperty, 0.0);
                this.SetValue(DisplayValueProperty, 0.0);

                UpdatePhaseVisuals(nextPhase);

                StartDotsAnimation(App.T(transitionMessageKey, transitionMessageKey));
                await Task.Delay(1500);
                StopDotsAnimation();

                GaugeValueText.Text = "0.0";
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        #endregion

        #region ================== MAIN TEST LOGIC ==================

        private async void StartTest_Click(object sender, RoutedEventArgs e)
        {
            if (_isTesting)
            {
                _cts?.Cancel();
                return;
            }

            _isTesting = true;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _service?.Dispose();
            _service = new SpeedTestService();

            ResetUI();
            SetTestingState(true);

            _service.ProgressChanged += OnProgressChanged;

            try
            {
                var token = _cts.Token;
                var result = await Task.Run(() => _service.RunFullTestAsync(token), token)
                                       .ConfigureAwait(true);
                ShowResults(result);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = App.T("SpeedTest_Cancelled", "Test cancelled.");
                SetTestingState(false);
                AnimateGauge(0);
            }
            catch (Exception ex)
            {
                StatusText.Text = App.T("SpeedTest_ErrorPrefix", "Error: {0}", ex.Message);
                SetTestingState(false);
            }
            finally
            {
                _isTesting = false;
                if (_service != null)
                {
                    _service.ProgressChanged -= OnProgressChanged;
                }
            }
        }

        private void OnProgressChanged(SpeedTestPhase phase, double value, string messageKey)
        {
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                if (!IsLoaded) return;

                string translatedMsg;
                if (messageKey.Contains('|'))
                {
                    var parts = messageKey.Split('|');
                    translatedMsg = App.T(parts[0], parts[0], parts[1]);
                }
                else
                {
                    translatedMsg = App.T(messageKey, messageKey);
                }

                if (phase != _lastRenderedPhase)
                {
                    if (_isTransitioning) return;

                    var previousPhase = _lastRenderedPhase;
                    _lastRenderedPhase = phase;
                    _currentPhase = phase;

                    if (phase == SpeedTestPhase.Download && previousPhase == SpeedTestPhase.Ping)
                    {
                        await PerformPhaseTransitionAsync(phase, "SpeedTest_GetReadyDownload");
                    }
                    else if (phase == SpeedTestPhase.Upload && previousPhase == SpeedTestPhase.Download)
                    {
                        await PerformPhaseTransitionAsync(phase, "SpeedTest_GetReadyUpload");
                    }
                    else
                    {
                        UpdatePhaseVisuals(phase);
                    }
                }

                if (_isTransitioning) return;

                StatusText.Text = translatedMsg;

                switch (phase)
                {
                    case SpeedTestPhase.Ping:
                        GaugeLabelText.Text = App.T("SpeedTest_Ping", "PING");
                        GaugeUnitText.Text = App.T("SpeedTest_UnitMs", " ms");
                        GaugeValueText.Text = $"{value:F0}";
                        break;

                    case SpeedTestPhase.Download:
                        GaugeLabelText.Text = App.T("SpeedTest_Download", "DOWNLOAD");
                        GaugeUnitText.Text = App.T("SpeedTest_UnitMbps", " Mbps");
                        _maxDownloadShown = Math.Max(_maxDownloadShown, value);
                        AnimateGauge(value);
                        DownloadValue.Text = $"{value:F2}";
                        DownloadValue.Foreground = new SolidColorBrush(DownloadColor);
                        break;

                    case SpeedTestPhase.Upload:
                        GaugeLabelText.Text = App.T("SpeedTest_Upload", "UPLOAD");
                        GaugeUnitText.Text = App.T("SpeedTest_UnitMbps", " Mbps");
                        _maxUploadShown = Math.Max(_maxUploadShown, value);
                        AnimateGauge(value);
                        UploadValue.Text = $"{value:F2}";
                        UploadValue.Foreground = new SolidColorBrush(UploadColor);
                        break;
                }

                var parent = ProgressBar.Parent as FrameworkElement;
                if (parent != null && parent.ActualWidth > 0)
                {
                    double progressPercent = 0;
                    switch (phase)
                    {
                        case SpeedTestPhase.Ping: progressPercent = Math.Min(10, value / 10.0); break;
                        case SpeedTestPhase.Download: progressPercent = 10 + (Math.Min(100, value) / 100.0 * 45); break;
                        case SpeedTestPhase.Upload: progressPercent = 55 + (Math.Min(100, value) / 100.0 * 45); break;
                    }
                    AnimateProgressBar(progressPercent / 100.0 * parent.ActualWidth);
                }
            }));
        }

        #endregion

        #region ================== RESULTS DISPLAY ==================

        private void ShowResults(SpeedTestResult r)
        {
            if (r == null) return;

            PingValue.Text = $"{r.PingMs:F0}";
            DownloadValue.Text = $"{r.DownloadMbps:F2}";
            UploadValue.Text = $"{r.UploadMbps:F2}";
            JitterValue.Text = $"{r.JitterMs:F1} ms";
            PacketLossValue.Text = $"{r.PacketLoss:F1} %";

            AnimateGauge(r.DownloadMbps);
            GaugeLabelText.Text = App.T("SpeedTest_GaugeComplete", "COMPLETE");
            GaugeUnitText.Text = App.T("SpeedTest_UnitMbps", " Mbps");
            if (GaugePhaseBadge != null)
                GaugePhaseBadge.Background = new SolidColorBrush(Color.FromArgb(40, 34, 197, 94));
            GaugeLabelText.Foreground = new SolidColorBrush(DownloadColor);
            GaugeArc.Stroke = (Brush)TryFindResource("SuccessGradient");
            if (GaugeNeedle != null)
                GaugeNeedle.Fill = new SolidColorBrush(DownloadColor);

            PublicIPText.Text = r.PublicIP ?? App.T("Common_NA", "N/A");
            LocalIPText.Text = r.LocalIP ?? App.T("Common_NA", "N/A");
            ConnTypeText.Text = r.ConnectionType ?? App.T("Common_NA", "N/A");
            ISPText.Text = r.ISP ?? App.T("Common_Unknown", "Unknown");
            ProviderUsedText.Text = r.ProviderUsed ?? App.T("Common_NA", "N/A");
            LocationText.Text = r.ServerLocation ?? App.T("Common_NA", "N/A");
            ServerText.Text = r.ServerName ?? r.ProviderUsed ?? App.T("Common_NA", "N/A");
            TestTimeText.Text = r.TestTime.ToString("yyyy-MM-dd HH:mm:ss");

            InfoSection.Visibility = Visibility.Visible;
            RatingSection.Visibility = Visibility.Visible;

            UpdateJitterRating(r.JitterMs);
            UpdatePacketLossRating(r.PacketLoss);

            _gamingRating = BuildGamingRating(r);
            _streamingRating = BuildStreamingRating(r);
            _videoCallRating = BuildVideoCallRating(r);
            _browsingRating = BuildBrowsingRating(r);

            ApplyRatingToCard(GamingRating, GamingScoreBar, GamingIcon, _gamingRating);
            ApplyRatingToCard(StreamingRating, StreamingScoreBar, StreamingIcon, _streamingRating);
            ApplyRatingToCard(VideoCallRating, VideoCallScoreBar, VideoCallIcon, _videoCallRating);
            ApplyRatingToCard(BrowsingRating, BrowsingScoreBar, BrowsingIcon, _browsingRating);

            StatusText.Text = App.T("SpeedTest_FinalStatus", "✓ Test complete! ↓{0:F1} / ↑{1:F1} Mbps",
                r.DownloadMbps, r.UploadMbps);

            var parent = ProgressBar.Parent as FrameworkElement;
            if (parent != null && parent.ActualWidth > 0)
                AnimateProgressBar(parent.ActualWidth);

            SetTestingState(false);
        }

        #endregion

        #region ================== RATING CARDS ==================

        private void ApplyRatingToCard(TextBlock label, Border scoreBar, TextBlock icon, ActivityRating rating)
        {
            if (label == null || scoreBar == null) return;

            label.Text = rating.ShortLabel;
            label.Foreground = new SolidColorBrush(rating.Color);

            if (icon != null)
                icon.Foreground = new SolidColorBrush(rating.Color);

            var anim = new DoubleAnimation
            {
                To = 50 * (rating.Score / 100.0),
                Duration = TimeSpan.FromMilliseconds(800),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            scoreBar.Background = new SolidColorBrush(rating.Color);
            scoreBar.BeginAnimation(WidthProperty, anim);
        }

        private ActivityRating BuildGamingRating(SpeedTestResult r)
        {
            var factors = new List<RatingFactor>();

            int pingScore = r.PingMs < 20 ? 100 : r.PingMs < 40 ? 85 : r.PingMs < 60 ? 65 : r.PingMs < 100 ? 40 : 15;
            factors.Add(new RatingFactor
            {
                Name = App.T("Rating_FactorPing", "Ping"),
                Value = $"{r.PingMs:F0} ms",
                Status = pingScore >= 85 ? App.T("Rating_StatusExcellent", "✓ Excellent") : pingScore >= 65 ? App.T("Rating_StatusGood", "✓ Good") : App.T("Rating_StatusHigh", "⚠ High"),
                Weight = 0.5,
                IsBottleneck = pingScore < 65
            });

            int jitterScore = r.JitterMs < 5 ? 100 : r.JitterMs < 15 ? 75 : r.JitterMs < 30 ? 45 : 20;
            factors.Add(new RatingFactor
            {
                Name = App.T("Rating_FactorJitter", "Jitter"),
                Value = $"{r.JitterMs:F1} ms",
                Status = jitterScore >= 75 ? App.T("Rating_StatusStable", "✓ Stable") : App.T("Rating_StatusUnstable", "⚠ Unstable"),
                Weight = 0.3,
                IsBottleneck = jitterScore < 50
            });

            int lossScore = r.PacketLoss == 0 ? 100 : r.PacketLoss < 1 ? 70 : r.PacketLoss < 3 ? 40 : 10;
            factors.Add(new RatingFactor
            {
                Name = App.T("Rating_FactorPacketLoss", "Packet Loss"),
                Value = $"{r.PacketLoss:F1}%",
                Status = lossScore == 100 ? App.T("Rating_StatusPerfect", "✓ Perfect") : App.T("Rating_StatusLossy", "⚠ Lossy"),
                Weight = 0.2,
                IsBottleneck = lossScore < 70
            });

            int totalScore = (int)(pingScore * 0.5 + jitterScore * 0.3 + lossScore * 0.2);

            string recommendation = totalScore >= 85 ? App.T("Rating_GamingPerfect", "Perfect for competitive gaming!")
                : pingScore < 65 ? App.T("Rating_GamingHighPing", "High ping may cause delay. Try wired connection.")
                : jitterScore < 50 ? App.T("Rating_GamingUnstable", "Unstable connection. Check WiFi interference.")
                : App.T("Rating_GamingLossy", "Packet loss detected. Contact ISP if persistent.");

            string summaryKey = totalScore >= 85 ? "Rating_GamingCompetitive" : totalScore >= 65 ? "Rating_GamingCasual" : "Rating_GamingSinglePlayer";
            string summarySuffix = App.T(summaryKey, summaryKey == "Rating_GamingCompetitive" ? "competitive" : summaryKey == "Rating_GamingCasual" ? "casual" : "single-player");

            return new ActivityRating
            {
                Title = App.T("Rating_GamingTitle", "Gaming"),
                Icon = "\uE7FC",
                Score = totalScore,
                ShortLabel = totalScore >= 85 ? App.T("Rating_Excellent", "Excellent") : totalScore >= 65 ? App.T("Rating_Good", "Good") : totalScore >= 40 ? App.T("Rating_Fair", "Fair") : App.T("Rating_Poor", "Poor"),
                Color = totalScore >= 85 ? Color.FromRgb(34, 197, 94) : totalScore >= 65 ? Color.FromRgb(14, 165, 233) : totalScore >= 40 ? Color.FromRgb(245, 158, 11) : Color.FromRgb(239, 68, 68),
                Summary = App.T("Rating_GamingSummary", "Suitable for {0} gaming.", summarySuffix),
                Factors = factors,
                Recommendation = recommendation
            };
        }

        private ActivityRating BuildStreamingRating(SpeedTestResult r)
        {
            int downloadScore = r.DownloadMbps >= 100 ? 100 : r.DownloadMbps >= 50 ? 90 : r.DownloadMbps >= 25 ? 80 : r.DownloadMbps >= 15 ? 65 : r.DownloadMbps >= 5 ? 45 : r.DownloadMbps >= 3 ? 30 : 15;
            int totalScore = downloadScore;

            string label = r.DownloadMbps >= 25 ? App.T("Rating_4KReady", "4K Ready") : r.DownloadMbps >= 15 ? App.T("Rating_1080pHD", "1080p HD") : r.DownloadMbps >= 5 ? App.T("Rating_720pHD", "720p HD") : r.DownloadMbps >= 3 ? App.T("Rating_SDOnly", "SD Only") : App.T("Rating_TooSlow", "Too Slow");

            return new ActivityRating
            {
                Title = App.T("Rating_StreamingTitle", "Streaming"),
                Icon = "\uE714",
                Score = totalScore,
                ShortLabel = label,
                Color = totalScore >= 80 ? Color.FromRgb(34, 197, 94) : totalScore >= 60 ? Color.FromRgb(14, 165, 233) : totalScore >= 40 ? Color.FromRgb(245, 158, 11) : Color.FromRgb(239, 68, 68),
                Summary = App.T("Rating_StreamingSummary", "{0} streaming quality.", label),
                Recommendation = r.DownloadMbps < 5 ? App.T("Rating_StreamingUpgrade", "Consider upgrading for HD streaming.") : App.T("Rating_StreamingGood", "Good for streaming!"),
                Factors = new List<RatingFactor>()
            };
        }

        private ActivityRating BuildVideoCallRating(SpeedTestResult r)
        {
            int score = (r.DownloadMbps >= 10 ? 50 : r.DownloadMbps >= 3 ? 30 : 10) +
                       (r.UploadMbps >= 5 ? 40 : r.UploadMbps >= 1 ? 25 : 10) +
                       (r.PingMs < 100 ? 10 : 0);

            string label = (r.DownloadMbps >= 10 && r.UploadMbps >= 5 && r.PingMs < 100) ? App.T("Rating_HDReady", "HD Ready") :
                          (r.DownloadMbps >= 3 && r.UploadMbps >= 1) ? App.T("Rating_SDReady", "SD Ready") : App.T("Rating_Poor", "Poor");

            return new ActivityRating
            {
                Title = App.T("Rating_VideoCallTitle", "Video Call"),
                Icon = "\uE8AA",
                Score = score,
                ShortLabel = label,
                Color = score >= 80 ? Color.FromRgb(34, 197, 94) : score >= 60 ? Color.FromRgb(14, 165, 233) : score >= 40 ? Color.FromRgb(245, 158, 11) : Color.FromRgb(239, 68, 68),
                Summary = App.T("Rating_VideoCallSummary", "{0} for video conferencing.", label),
                Recommendation = r.UploadMbps < 3 ? App.T("Rating_VideoCallLowUpload", "Low upload may affect video quality.") : App.T("Rating_VideoCallGood", "Good for calls!"),
                Factors = new List<RatingFactor>()
            };
        }

        private ActivityRating BuildBrowsingRating(SpeedTestResult r)
        {
            int score = r.DownloadMbps >= 50 ? 100 : r.DownloadMbps >= 25 ? 85 : r.DownloadMbps >= 10 ? 70 : r.DownloadMbps >= 5 ? 50 : r.DownloadMbps >= 1 ? 30 : 10;

            string label = r.DownloadMbps >= 10 ? App.T("Rating_Fast", "Fast") : r.DownloadMbps >= 5 ? App.T("Rating_Good", "Good") : r.DownloadMbps >= 1 ? App.T("Rating_Slow", "Slow") : App.T("Rating_VerySlow", "Very Slow");

            return new ActivityRating
            {
                Title = App.T("Rating_BrowsingTitle", "Browsing"),
                Icon = "\uE774",
                Score = score,
                ShortLabel = label,
                Color = score >= 70 ? Color.FromRgb(34, 197, 94) : score >= 50 ? Color.FromRgb(14, 165, 233) : score >= 30 ? Color.FromRgb(245, 158, 11) : Color.FromRgb(239, 68, 68),
                Summary = App.T("Rating_BrowsingSummary", "{0} browsing experience.", label),
                Recommendation = r.DownloadMbps < 5 ? App.T("Rating_BrowsingSlow", "Pages may load slowly.") : App.T("Rating_BrowsingSmooth", "Smooth browsing!"),
                Factors = new List<RatingFactor>()
            };
        }

        #endregion

        #region ================== RATINGS HELPERS ==================

        private void UpdateJitterRating(double jitter)
        {
            string rating; Color color;
            if (jitter < 5) { rating = App.T("Rating_Excellent", "Excellent"); color = Color.FromRgb(34, 197, 94); }
            else if (jitter < 15) { rating = App.T("Rating_Good", "Good"); color = Color.FromRgb(14, 165, 233); }
            else if (jitter < 30) { rating = App.T("Rating_Fair", "Fair"); color = Color.FromRgb(245, 158, 11); }
            else { rating = App.T("Rating_Poor", "Poor"); color = Color.FromRgb(239, 68, 68); }

            if (JitterRating.Child is TextBlock tb)
            {
                tb.Text = rating;
                tb.Foreground = new SolidColorBrush(color);
            }
            JitterRating.Background = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B));
        }

        private void UpdatePacketLossRating(double loss)
        {
            string rating; Color color;
            if (loss == 0) { rating = App.T("Rating_Excellent", "Excellent"); color = Color.FromRgb(34, 197, 94); }
            else if (loss < 1) { rating = App.T("Rating_Good", "Good"); color = Color.FromRgb(14, 165, 233); }
            else if (loss < 3) { rating = App.T("Rating_Fair", "Fair"); color = Color.FromRgb(245, 158, 11); }
            else { rating = App.T("Rating_Poor", "Poor"); color = Color.FromRgb(239, 68, 68); }

            if (PacketLossRating.Child is TextBlock tb)
            {
                tb.Text = rating;
                tb.Foreground = new SolidColorBrush(color);
            }
            PacketLossRating.Background = new SolidColorBrush(Color.FromArgb(30, color.R, color.G, color.B));
        }

        #endregion

        #region ================== ANIMATIONS ==================

        private void AnimateGauge(double mbps)
        {
            bool isStartingFresh = _currentDisplayValue < 0.5 && mbps > 1;
            var duration = isStartingFresh ? 800 : 500;

            var anim = new DoubleAnimation
            {
                To = mbps,
                Duration = TimeSpan.FromMilliseconds(duration),
                EasingFunction = isStartingFresh
                    ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
                    : (IEasingFunction)new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(GaugeProgressProperty, anim);

            var textAnim = new DoubleAnimation
            {
                To = mbps,
                Duration = TimeSpan.FromMilliseconds(duration),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            this.BeginAnimation(DisplayValueProperty, textAnim);
        }

        private void AnimateProgressBar(double targetWidth)
        {
            var anim = new DoubleAnimation
            {
                To = Math.Max(0, targetWidth),
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBar.BeginAnimation(WidthProperty, anim);
        }

        #endregion

        #region ================== UI STATE ==================

        private void SetTestingState(bool testing)
        {
            _isTesting = testing;
            StartBtnIcon.Text = testing ? "\uE71A" : "\uE768";
            StartBtnText.Text = testing ? App.T("SpeedTest_CancelBtn", "Cancel") : App.T("SpeedTest_StartTest", "Start Test");
            if (testing)
                GaugeLabelText.Text = App.T("SpeedTest_Testing", "TESTING...");
        }

        private void ResetUI()
        {
            PingValue.Text = "--";
            DownloadValue.Text = "--";
            UploadValue.Text = "--";
            JitterValue.Text = "-- ms";
            PacketLossValue.Text = "-- %";
            GaugeValueText.Text = "0.0";
            GaugeUnitText.Text = App.T("SpeedTest_UnitMbps", " Mbps");
            GaugeLabelText.Text = App.T("SpeedTest_GaugeReady", "READY");
            InfoSection.Visibility = Visibility.Collapsed;
            RatingSection.Visibility = Visibility.Collapsed;

            _maxDownloadShown = 0;
            _maxUploadShown = 0;
            _currentPhase = SpeedTestPhase.Ping;

            _isTransitioning = false;
            _lastRenderedPhase = SpeedTestPhase.Ping;
            StopDotsAnimation();

            GaugeArc.Stroke = (Brush)TryFindResource("AccentGradient");
            if (GaugeNeedle != null)
                GaugeNeedle.Fill = (Brush)TryFindResource("DynamicAccent");

            if (GaugePhaseBadge != null)
                GaugePhaseBadge.Background = new SolidColorBrush(Color.FromArgb(40, 14, 165, 233));
            if (GaugeLabelText != null)
                GaugeLabelText.Foreground = (Brush)TryFindResource("DynamicAccent");

            AnimateProgressBar(0);
            AnimateGauge(0);
        }

        #endregion

        #region ================== WINDOW EVENTS ==================

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            Close();
        }

        #endregion
    }
}