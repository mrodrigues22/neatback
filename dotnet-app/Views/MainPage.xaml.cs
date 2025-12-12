using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NeatBack.Models;
using NeatBack.Services;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Microsoft.UI.Dispatching;

namespace NeatBack.Views;

public sealed partial class MainPage : Page
{
    private WebSocketClient? _wsClient;
    private NotificationService? _notificationService;
    private bool _isMonitoring = false;
    private Image? _cameraPreview;
    private Border? _cameraBorder;
    private Microsoft.UI.Xaml.Media.Animation.Storyboard? _pulseAnimation;
    private Microsoft.UI.Xaml.Media.Animation.Storyboard? _spinAnimation;
    private FontIcon? _statusIcon;
    private Border? _pitchCard;
    private Border? _rollCard;
    private Border? _shoulderCard;
    private Border? _distanceCard;
    private Border? _badDurationCard;
    private string _currentPostureStatus = "neutral";
    // Local references bound via FindName to avoid reliance on generated fields
    private TextBlock? _statusText;
    private ProgressBar? _badPostureProgress;
    private TextBlock? _pitchText;
    private TextBlock? _rollText;
    private TextBlock? _shoulderTiltText;
    private TextBlock? _distanceText;
    private TextBlock? _badDurationText;
    private Button? _startButton;
    private Button? _savePostureButton;
    private Button? _muteButton;
    private Slider? _pitchThresholdSlider;
    private Slider? _distanceThresholdSlider;
    private Slider? _headRollThresholdSlider;
    private Slider? _shoulderTiltThresholdSlider;
    private TextBlock? _pitchThresholdValue;
    private TextBlock? _distanceThresholdValue;
    private TextBlock? _headRollThresholdValue;
    private TextBlock? _shoulderTiltThresholdValue;
    
    public MainPage()
    {
        this.InitializeComponent();
        _wsClient = new WebSocketClient();
        _notificationService = new NotificationService();
        // Bind local references to XAML controls
        _cameraPreview = FindName("CameraPreview") as Image;
        _cameraBorder = FindName("CameraBorder") as Border;
        _pulseAnimation = FindName("PulseAnimation") as Microsoft.UI.Xaml.Media.Animation.Storyboard;
        _spinAnimation = FindName("SpinAnimation") as Microsoft.UI.Xaml.Media.Animation.Storyboard;
        _statusIcon = FindName("StatusIcon") as FontIcon;
        _pitchCard = FindName("PitchCard") as Border;
        _rollCard = FindName("RollCard") as Border;
        _shoulderCard = FindName("ShoulderCard") as Border;
        _distanceCard = FindName("DistanceCard") as Border;
        _badDurationCard = FindName("BadDurationCard") as Border;
        _statusText = FindName("StatusText") as TextBlock;
        _badPostureProgress = FindName("BadPostureProgress") as ProgressBar;
        _pitchText = FindName("PitchText") as TextBlock;
        _rollText = FindName("RollText") as TextBlock;
        _shoulderTiltText = FindName("ShoulderTiltText") as TextBlock;
        _distanceText = FindName("DistanceText") as TextBlock;
        _badDurationText = FindName("BadDurationText") as TextBlock;
        _startButton = FindName("StartButton") as Button;
        _savePostureButton = FindName("SavePostureButton") as Button;
        _muteButton = FindName("MuteButton") as Button;
        _pitchThresholdSlider = FindName("PitchThresholdSlider") as Slider;
        _distanceThresholdSlider = FindName("DistanceThresholdSlider") as Slider;
        _headRollThresholdSlider = FindName("HeadRollThresholdSlider") as Slider;
        _shoulderTiltThresholdSlider = FindName("ShoulderTiltThresholdSlider") as Slider;
        _pitchThresholdValue = FindName("PitchThresholdValue") as TextBlock;
        _distanceThresholdValue = FindName("DistanceThresholdValue") as TextBlock;
        _headRollThresholdValue = FindName("HeadRollThresholdValue") as TextBlock;
        _shoulderTiltThresholdValue = FindName("ShoulderTiltThresholdValue") as TextBlock;
        
        // Subscribe to events
        _wsClient.PostureDataReceived += OnPostureDataReceived;
        _wsClient.PostureSaved += OnPostureSaved;
        _wsClient.ThresholdsUpdated += OnThresholdsUpdated;
    }
    
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isMonitoring)
            {
                // Connect to WebSocket and start monitoring
                if (_statusText != null) _statusText.Text = "Connecting to service...";
                
                await _wsClient!.ConnectAsync();
                await _wsClient!.StartMonitoringAsync();
                
                _isMonitoring = true;
                if (_startButton != null)
                {
                    _startButton.Content = "⏹ Stop Monitoring";
                    _startButton.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
                }
                if (_savePostureButton != null) _savePostureButton.IsEnabled = true;
                if (_statusText != null) _statusText.Text = "Monitoring started. Please save your good posture!";
                
                System.Diagnostics.Debug.WriteLine("Monitoring started successfully");
            }
            else
            {
                // Stop monitoring
                await StopMonitoring();
            }
        }
        catch (Exception ex)
        {
            if (_statusText != null) _statusText.Text = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error starting monitoring: {ex}");
        }
    }
    
    private async Task StopMonitoring()
    {
        _isMonitoring = false;
        
        // Stop monitoring on backend
        if (_wsClient != null)
        {
            await _wsClient.StopMonitoringAsync();
            await _wsClient.DisconnectAsync();
        }
        
        // Clear preview
        if (_cameraPreview != null)
        {
            _cameraPreview.Source = null;
        }
        
        if (_startButton != null)
        {
            _startButton.Content = "▶ Start Monitoring";
            _startButton.ClearValue(Button.ForegroundProperty);
        }
        if (_savePostureButton != null) _savePostureButton.IsEnabled = false;
        if (_statusText != null) _statusText.Text = "Monitoring stopped";
        if (_pitchText != null) _pitchText.Text = "--°";
        if (_rollText != null) _rollText.Text = "--°";
        if (_shoulderTiltText != null) _shoulderTiltText.Text = "--°";
        if (_distanceText != null) _distanceText.Text = "-- in";
        if (_badDurationText != null) _badDurationText.Text = "0 s";
        if (_badPostureProgress != null)
        {
            _badPostureProgress.Value = 0;
            _badPostureProgress.Visibility = Visibility.Collapsed;
        }
    }
    
    private async void SavePostureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_wsClient != null)
        {
            if (_statusText != null) _statusText.Text = "Saving good posture...";
            await _wsClient.SaveGoodPostureAsync();
        }
    }
    
    private void OnPostureDataReceived(object? sender, PostureData data)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            // Update webcam preview if frame data is available
            if (!string.IsNullOrEmpty(data.Frame) && _cameraPreview != null)
            {
                try
                {
                    byte[] imageBytes = Convert.FromBase64String(data.Frame);
                    using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    await stream.WriteAsync(imageBytes.AsBuffer());
                    stream.Seek(0);
                    
                    var bitmapImage = new BitmapImage();
                    await bitmapImage.SetSourceAsync(stream);
                    _cameraPreview.Source = bitmapImage;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error displaying frame: {ex.Message}");
                }
            }
            
            // Update metrics
            if (data.AdjustedPitch.HasValue)
            {
                if (_pitchText != null) _pitchText.Text = $"{data.AdjustedPitch.Value:F1}°";
            }
            
            if (data.AdjustedRoll.HasValue)
            {
                if (_rollText != null) _rollText.Text = $"{data.AdjustedRoll.Value:F1}°";
            }
            
            if (data.AdjustedShoulderTilt.HasValue)
            {
                if (_shoulderTiltText != null) _shoulderTiltText.Text = $"{data.AdjustedShoulderTilt.Value:F1}°";
            }
            
            if (data.Distance.HasValue)
            {
                // Convert cm to inches for display
                var inches = data.Distance.Value / 2.54;
                if (_distanceText != null) _distanceText.Text = $"{inches:F1} in";
            }
            
            if (_badDurationText != null) _badDurationText.Text = $"{data.BadDuration} s";
            
            // Update status and camera border color
            if (!string.IsNullOrEmpty(data.Error))
            {
                if (_statusText != null) _statusText.Text = data.Error;
                UpdatePostureVisuals("neutral");
            }
            else if (data.IsBad)
            {
                if (_statusText != null) _statusText.Text = $"⚠️ {data.Message}";
                if (_badPostureProgress != null)
                {
                    _badPostureProgress.Visibility = Visibility.Visible;
                    _badPostureProgress.Value = Math.Min(data.BadDuration, 30);
                }
                // Change border to yellow if bad but not warning yet, red if warning
                UpdatePostureVisuals(data.ShouldWarn ? "bad" : "warning");
                
                // Highlight problematic metrics
                HighlightProblematicMetrics(data);
            }
            else
            {
                if (_statusText != null) _statusText.Text = "✅ Good posture";
                if (_badPostureProgress != null)
                {
                    _badPostureProgress.Visibility = Visibility.Collapsed;
                    _badPostureProgress.Value = 0;
                }
                UpdatePostureVisuals("good");
                ResetMetricHighlights();
            }
            
            // Send notification if needed
            if (data.ShouldWarn && _notificationService != null && !_notificationService.IsMuted)
            {
                _notificationService.ShowAlert($"Bad posture for {data.BadDuration} seconds! Please adjust.");
            }
        });
    }
    
    private void OnPostureSaved(object? sender, bool success)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (success)
            {
                if (_statusText != null) _statusText.Text = "✅ Good posture saved! Monitoring...";
            }
            else
            {
                if (_statusText != null) _statusText.Text = "❌ Failed to save posture. Make sure your face is visible.";
            }
        });
    }
    
    private void UpdatePostureVisuals(string status)
    {
        if (_cameraBorder == null) return;
        
        // Update border color
        var brush = status switch
        {
            "good" => (Microsoft.UI.Xaml.Media.Brush)this.Resources["GoodPostureBrush"],
            "warning" => (Microsoft.UI.Xaml.Media.Brush)this.Resources["WarningPostureBrush"],
            "bad" => (Microsoft.UI.Xaml.Media.Brush)this.Resources["BadPostureBrush"],
            _ => (Microsoft.UI.Xaml.Media.Brush)this.Resources["NeutralPostureBrush"]
        };
        _cameraBorder.BorderBrush = brush;
        
        // Control pulse animation
        if (status == "bad" && _currentPostureStatus != "bad")
        {
            _pulseAnimation?.Begin();
        }
        else if (status != "bad" && _currentPostureStatus == "bad")
        {
            _pulseAnimation?.Stop();
            if (_cameraBorder != null) _cameraBorder.Opacity = 1.0;
        }
        
        // Update status icon and animation
        if (_statusIcon != null)
        {
            _statusIcon.Glyph = status switch
            {
                "good" => "\uE73E",  // Checkmark circle
                "warning" => "\uE7BA",  // Warning
                "bad" => "\uE711",  // Error
                _ => "\uE8F4"  // Info
            };
            
            // Spin icon when processing
            if (status == "warning" || status == "bad")
            {
                if (_currentPostureStatus != status)
                {
                    AnimateCardPop(_badDurationCard);
                }
            }
        }
        
        _currentPostureStatus = status;
    }
    
    private void HighlightProblematicMetrics(PostureData data)
    {
        // Highlight cards for metrics that are problematic
        var issues = data.PostureIssues ?? new List<string>();
        
        foreach (var issue in issues)
        {
            if (issue.Contains("pitch", StringComparison.OrdinalIgnoreCase) || issue.Contains("forward", StringComparison.OrdinalIgnoreCase))
            {
                AnimateCardPop(_pitchCard);
            }
            if (issue.Contains("roll", StringComparison.OrdinalIgnoreCase) || issue.Contains("tilting", StringComparison.OrdinalIgnoreCase))
            {
                AnimateCardPop(_rollCard);
            }
            if (issue.Contains("shoulder", StringComparison.OrdinalIgnoreCase))
            {
                AnimateCardPop(_shoulderCard);
            }
            if (issue.Contains("distance", StringComparison.OrdinalIgnoreCase) || issue.Contains("close", StringComparison.OrdinalIgnoreCase))
            {
                AnimateCardPop(_distanceCard);
            }
        }
    }
    
    private void ResetMetricHighlights()
    {
        // Reset all card scales to normal
        ResetCardScale(_pitchCard);
        ResetCardScale(_rollCard);
        ResetCardScale(_shoulderCard);
        ResetCardScale(_distanceCard);
    }
    
    private void AnimateCardPop(Border? card)
    {
        if (card == null) return;
        
        var scaleTransform = new Microsoft.UI.Xaml.Media.ScaleTransform
        {
            CenterX = card.ActualWidth / 2,
            CenterY = card.ActualHeight / 2
        };
        card.RenderTransform = scaleTransform;
        
        var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        var scaleXAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 1.0,
            To = 1.08,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            AutoReverse = true,
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.BackEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
        };
        var scaleYAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 1.0,
            To = 1.08,
            Duration = new Duration(TimeSpan.FromMilliseconds(200)),
            AutoReverse = true,
            EasingFunction = new Microsoft.UI.Xaml.Media.Animation.BackEase { EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut }
        };
        
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleXAnim, scaleTransform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleXAnim, "ScaleX");
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(scaleYAnim, scaleTransform);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(scaleYAnim, "ScaleY");
        
        storyboard.Children.Add(scaleXAnim);
        storyboard.Children.Add(scaleYAnim);
        storyboard.Begin();
    }
    
    private void ResetCardScale(Border? card)
    {
        if (card == null) return;
        card.RenderTransform = null;
    }
    
    private void OnThresholdsUpdated(object? sender, bool success)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (success)
            {
                if (_statusText != null) _statusText.Text = "Thresholds updated";
            }
        });
    }
    
    private async void ThresholdSlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!_isMonitoring)
            return;
        
        var pitchThreshold = _pitchThresholdSlider != null ? _pitchThresholdSlider.Value : -10;
        var distanceThreshold = _distanceThresholdSlider != null ? _distanceThresholdSlider.Value : 10;
        var headRollThreshold = _headRollThresholdSlider != null ? _headRollThresholdSlider.Value : 15;
        var shoulderTiltThreshold = _shoulderTiltThresholdSlider != null ? _shoulderTiltThresholdSlider.Value : 10;
        
        if (_pitchThresholdValue != null)
            _pitchThresholdValue.Text = $"{pitchThreshold:F0}°";
        if (_distanceThresholdValue != null)
        {
            var inches = distanceThreshold / 2.54;
            _distanceThresholdValue.Text = $"{inches:F0} in";
        }
        if (_headRollThresholdValue != null)
            _headRollThresholdValue.Text = $"{headRollThreshold:F0}°";
        if (_shoulderTiltThresholdValue != null)
            _shoulderTiltThresholdValue.Text = $"{shoulderTiltThreshold:F0}°";
        
        if (_wsClient != null)
        {
            await _wsClient.SetThresholdsAsync(pitchThreshold, distanceThreshold, headRollThreshold, shoulderTiltThreshold);
        }
    }
    
    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_notificationService != null)
        {
            _notificationService.IsMuted = !_notificationService.IsMuted;
            
            if (_muteButton != null)
            {
                _muteButton.Content = _notificationService.IsMuted ? "🔕" : "🔔";
                ToolTipService.SetToolTip(_muteButton, 
                    _notificationService.IsMuted ? "Unmute notifications" : "Mute notifications");
            }
            
            if (_statusText != null)
            {
                _statusText.Text = _notificationService.IsMuted 
                    ? "🔕 Notifications muted" 
                    : "🔔 Notifications enabled";
            }
        }
    }
}

