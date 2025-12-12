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
        _statusText = FindName("StatusText") as TextBlock;
        _badPostureProgress = FindName("BadPostureProgress") as ProgressBar;
        _pitchText = FindName("PitchText") as TextBlock;
        _rollText = FindName("RollText") as TextBlock;
        _shoulderTiltText = FindName("ShoulderTiltText") as TextBlock;
        _distanceText = FindName("DistanceText") as TextBlock;
        _badDurationText = FindName("BadDurationText") as TextBlock;
        _startButton = FindName("StartButton") as Button;
        _savePostureButton = FindName("SavePostureButton") as Button;
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
            
            // Update status
            if (!string.IsNullOrEmpty(data.Error))
            {
                if (_statusText != null) _statusText.Text = data.Error;
            }
            else if (data.IsBad)
            {
                if (_statusText != null) _statusText.Text = $"⚠️ {data.Message}";
                if (_badPostureProgress != null)
                {
                    _badPostureProgress.Visibility = Visibility.Visible;
                    _badPostureProgress.Value = Math.Min(data.BadDuration, 30);
                }
            }
            else
            {
                if (_statusText != null) _statusText.Text = "✅ Good posture";
                if (_badPostureProgress != null)
                {
                    _badPostureProgress.Visibility = Visibility.Collapsed;
                    _badPostureProgress.Value = 0;
                }
            }
            
            // Send notification if needed
            if (data.ShouldWarn)
            {
                _notificationService?.ShowAlert($"Bad posture for {data.BadDuration} seconds! Please adjust.");
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
}

