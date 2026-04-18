using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace KaraokeApp.Windows
{
    public partial class SplashScreenWindow : Window
    {
        private double _totalWidth;

        public SplashScreenWindow()
        {
            InitializeComponent();
            Loaded += SplashScreenWindow_Loaded;
        }

        private async void SplashScreenWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fade in the window
            Opacity = 0;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(600));
            BeginAnimation(OpacityProperty, fadeIn);

            await Task.Delay(600);

            // Get the actual track width after layout
            _totalWidth = ProgressFill.Parent is System.Windows.Controls.Border track
                ? track.ActualWidth
                : 520;

            // Run the loading steps
            await RunStep("Loading database...", 0.20, 400);
            await RunStep("Loading categories...", 0.40, 300);
            await RunStep("Initializing media player...", 0.65, 500);
            await RunStep("Loading song library...", 0.85, 300);
            await RunStep("Almost ready...", 1.00, 400);

            await Task.Delay(300);

            // Fade out then close
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400));
            fadeOut.Completed += (s, ev) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        private async Task RunStep(string status, double progress, int delayMs)
        {
            StatusText.Text = status;

            // Animate progress bar width
            double targetWidth = _totalWidth * progress;
            var anim = new DoubleAnimation(ProgressFill.Width, targetWidth,
                TimeSpan.FromMilliseconds(delayMs - 80))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, anim);

            await Task.Delay(delayMs);
        }
    }
}