using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Slouti.Models;
using Slouti.Services;
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Microsoft.UI.Dispatching;

namespace Slouti.Views;

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
    private Grid? _loadingOverlay;
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
    private Slider? _overallSensitivitySlider;
    private TextBlock? _overallSensitivityValue;
    private StackPanel? _simpleSensitivityPanel;
    private StackPanel? _advancedSensitivityPanel;
    private Button? _showAdvancedButton;
    private Button? _hideAdvancedButton;
    private bool _isUpdatingSliders = false;
    
    // Helper method to format scale value for display
    private string ScaleToLabel(double scale)
    {
        // Format to one decimal place
        return $"{scale:F1}";
    }
    
    public MainPage()
    {
        this.InitializeComponent();
        _wsClient = new WebSocketClient();
        _notificationService = new NotificationService();
        // Bind local references to XAML controls
        _cameraPreview = FindName("CameraPreview") as Image;
        _cameraBorder = FindName("CameraBorder") as Border;
        _loadingOverlay = FindName("LoadingOverlay") as Grid;
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
        _overallSensitivitySlider = FindName("OverallSensitivitySlider") as Slider;
        _overallSensitivityValue = FindName("OverallSensitivityValue") as TextBlock;
        _simpleSensitivityPanel = FindName("SimpleSensitivityPanel") as StackPanel;
        _advancedSensitivityPanel = FindName("AdvancedSensitivityPanel") as StackPanel;
        _showAdvancedButton = FindName("ShowAdvancedButton") as Button;
        _hideAdvancedButton = FindName("HideAdvancedButton") as Button;
        
        // Subscribe to events
        _wsClient.PostureDataReceived += OnPostureDataReceived;
        _wsClient.PostureSaved += OnPostureSaved;
        _wsClient.ThresholdsUpdated += OnThresholdsUpdated;
        _wsClient.MonitoringStarted += OnMonitoringStarted;
    }
    
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_isMonitoring)
            {
                // Mark as monitoring to prevent double-clicks
                _isMonitoring = true;
                
                // Update button immediately to show monitoring state
                if (_startButton != null)
                {
                    _startButton.Content = "⏹ Stop Monitoring";
                    _startButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 211, 211, 211));
                    _startButton.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black);
                }
                
                // Enable save posture button immediately
                if (_savePostureButton != null) _savePostureButton.IsEnabled = true;
                
                // Show loading spinner
                if (_loadingOverlay != null) _loadingOverlay.Visibility = Visibility.Visible;
                
                // Connect to WebSocket and start monitoring
                if (_statusText != null) _statusText.Text = "Starting...";
                
                await _wsClient!.ConnectAsync();
                await _wsClient!.StartMonitoringAsync();
                
                // Status text will update in OnMonitoringStarted event handler
                // Status remains "Starting..." until camera is ready
            }
            else
            {
                // Stop monitoring
                await StopMonitoring();
            }
        }
        catch (Exception ex)
        {
            // Reset monitoring state on error
            _isMonitoring = false;
            
            // Hide loading spinner on error
            if (_loadingOverlay != null) _loadingOverlay.Visibility = Visibility.Collapsed;
            
            // Reset button
            if (_startButton != null)
            {
                _startButton.Content = "▶ Start Monitoring";
                _startButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
                _startButton.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            }
            
            if (_statusText != null) _statusText.Text = $"Error: {ex.Message}";
        }
    }
    
    private async Task StopMonitoring()
    {
        _isMonitoring = false;
        
        // Hide loading spinner if still visible
        if (_loadingOverlay != null) _loadingOverlay.Visibility = Visibility.Collapsed;
        
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
            _startButton.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
            _startButton.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
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
    
    private void OnMonitoringStarted(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // Button was already updated in StartButton_Click
            // Just update status and enable save button
            if (_savePostureButton != null) _savePostureButton.IsEnabled = true;
            if (_statusText != null) _statusText.Text = "Monitoring started. Please save your good posture!";
        });
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
                    // Hide loading spinner when first frame is received
                    if (_loadingOverlay != null && _loadingOverlay.Visibility == Visibility.Visible)
                    {
                        _loadingOverlay.Visibility = Visibility.Collapsed;
                    }
                    
                    byte[] imageBytes = Convert.FromBase64String(data.Frame);
                    using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    await stream.WriteAsync(imageBytes.AsBuffer());
                    stream.Seek(0);
                    
                    // Optimize bitmap creation with DecodePixelWidth for faster rendering
                    var bitmapImage = new BitmapImage
                    {
                        DecodePixelWidth = 640  // Match preview frame size for optimal performance
                    };
                    await bitmapImage.SetSourceAsync(stream);
                    _cameraPreview.Source = bitmapImage;
                }
                catch (Exception ex)
                {
                    // Error displaying frame
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
                // Use the detailed message from the Python service
                string message = !string.IsNullOrEmpty(data.Message) 
                    ? data.Message 
                    : $"Bad posture for {data.BadDuration} seconds! Please adjust.";
                _notificationService.ShowAlert(message);
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
        
        // Get scale values (1.0-5.0) from sliders
        var pitchScale = _pitchThresholdSlider != null ? _pitchThresholdSlider.Value : 3.0;
        var distanceScale = _distanceThresholdSlider != null ? _distanceThresholdSlider.Value : 3.0;
        var headRollScale = _headRollThresholdSlider != null ? _headRollThresholdSlider.Value : 3.0;
        var shoulderTiltScale = _shoulderTiltThresholdSlider != null ? _shoulderTiltThresholdSlider.Value : 3.0;
        
        // Update UI labels with sensitivity levels
        if (_pitchThresholdValue != null)
            _pitchThresholdValue.Text = ScaleToLabel(pitchScale);
        if (_distanceThresholdValue != null)
            _distanceThresholdValue.Text = ScaleToLabel(distanceScale);
        if (_headRollThresholdValue != null)
            _headRollThresholdValue.Text = ScaleToLabel(headRollScale);
        if (_shoulderTiltThresholdValue != null)
            _shoulderTiltThresholdValue.Text = ScaleToLabel(shoulderTiltScale);
        
        // Send sensitivity scales directly to Python service
        // Python service will convert them to actual threshold values (single source of truth)
        if (_wsClient != null)
        {
            await _wsClient.SetThresholdsAsync(
                pitchScale,
                distanceScale,
                headRollScale,
                shoulderTiltScale
            );
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
    
    private async void OverallSensitivitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_isUpdatingSliders || _overallSensitivitySlider == null)
            return;
        
        double overallScale = _overallSensitivitySlider.Value;
        
        // Update label
        if (_overallSensitivityValue != null)
            _overallSensitivityValue.Text = ScaleToLabel(overallScale);
        
        // Update all individual sliders to match
        _isUpdatingSliders = true;
        if (_pitchThresholdSlider != null) _pitchThresholdSlider.Value = overallScale;
        if (_distanceThresholdSlider != null) _distanceThresholdSlider.Value = overallScale;
        if (_headRollThresholdSlider != null) _headRollThresholdSlider.Value = overallScale;
        if (_shoulderTiltThresholdSlider != null) _shoulderTiltThresholdSlider.Value = overallScale;
        _isUpdatingSliders = false;
        
        // Update individual labels
        if (_pitchThresholdValue != null) _pitchThresholdValue.Text = ScaleToLabel(overallScale);
        if (_distanceThresholdValue != null) _distanceThresholdValue.Text = ScaleToLabel(overallScale);
        if (_headRollThresholdValue != null) _headRollThresholdValue.Text = ScaleToLabel(overallScale);
        if (_shoulderTiltThresholdValue != null) _shoulderTiltThresholdValue.Text = ScaleToLabel(overallScale);
        
        // Send to WebSocket if monitoring
        if (_isMonitoring && _wsClient != null)
        {
            await _wsClient.SetThresholdsAsync(
                overallScale,
                overallScale,
                overallScale,
                overallScale
            );
        }
    }
    
    private void ShowAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_simpleSensitivityPanel != null)
            _simpleSensitivityPanel.Visibility = Visibility.Collapsed;
        if (_advancedSensitivityPanel != null)
            _advancedSensitivityPanel.Visibility = Visibility.Visible;
    }
    
    private void HideAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        if (_advancedSensitivityPanel != null)
            _advancedSensitivityPanel.Visibility = Visibility.Collapsed;
        if (_simpleSensitivityPanel != null)
            _simpleSensitivityPanel.Visibility = Visibility.Visible;
        
        // When returning to simple mode, sync overall slider with average of individual sliders
        if (_pitchThresholdSlider != null && _distanceThresholdSlider != null && 
            _headRollThresholdSlider != null && _shoulderTiltThresholdSlider != null &&
            _overallSensitivitySlider != null)
        {
            double average = (_pitchThresholdSlider.Value + _distanceThresholdSlider.Value + 
                            _headRollThresholdSlider.Value + _shoulderTiltThresholdSlider.Value) / 4.0;
            int roundedAverage = (int)Math.Round(average);
            
            _isUpdatingSliders = true;
            _overallSensitivitySlider.Value = roundedAverage;
            if (_overallSensitivityValue != null)
                _overallSensitivityValue.Text = ScaleToLabel(roundedAverage);
            _isUpdatingSliders = false;
        }
    }
}

