using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NeatBack.Models;
using NeatBack.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;

namespace NeatBack.Views;

public sealed partial class MainPage : Page
{
    private WebSocketClient? _wsClient;
    private NotificationService? _notificationService;
    private bool _isMonitoring = false;
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private Image? _cameraPreview;
    private CancellationTokenSource? _previewCts;
    // Local references bound via FindName to avoid reliance on generated fields
    private TextBlock? _statusText;
    private ProgressBar? _badPostureProgress;
    private TextBlock? _pitchText;
    private TextBlock? _distanceText;
    private TextBlock? _badDurationText;
    private Button? _startButton;
    private Button? _savePostureButton;
    private Slider? _pitchThresholdSlider;
    private Slider? _distanceThresholdSlider;
    private TextBlock? _pitchThresholdValue;
    private TextBlock? _distanceThresholdValue;
    
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
        _distanceText = FindName("DistanceText") as TextBlock;
        _badDurationText = FindName("BadDurationText") as TextBlock;
        _startButton = FindName("StartButton") as Button;
        _savePostureButton = FindName("SavePostureButton") as Button;
        _pitchThresholdSlider = FindName("PitchThresholdSlider") as Slider;
        _distanceThresholdSlider = FindName("DistanceThresholdSlider") as Slider;
        _pitchThresholdValue = FindName("PitchThresholdValue") as TextBlock;
        _distanceThresholdValue = FindName("DistanceThresholdValue") as TextBlock;
        
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
                // Start camera preview first
                if (_statusText != null) _statusText.Text = "Starting camera...";
                await StartCameraAsync();
                
                // Connect to WebSocket and start monitoring
                if (_statusText != null) _statusText.Text = "Connecting to service...";
                
                await _wsClient!.ConnectAsync();
                await _wsClient!.StartMonitoringAsync();
                
                _isMonitoring = true;
                if (_startButton != null) _startButton.Content = "Stop Monitoring";
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
        
        // Stop camera
        StopCamera();
        
        if (_startButton != null) _startButton.Content = "Start Monitoring";
        if (_savePostureButton != null) _savePostureButton.IsEnabled = false;
        if (_statusText != null) _statusText.Text = "Monitoring stopped";
        if (_pitchText != null) _pitchText.Text = "--°";
        if (_distanceText != null) _distanceText.Text = "-- cm";
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
        DispatcherQueue.TryEnqueue(() =>
        {
            // Update metrics
            if (data.AdjustedPitch.HasValue)
            {
                if (_pitchText != null) _pitchText.Text = $"{data.AdjustedPitch.Value:F1}°";
            }
            
            if (data.Distance.HasValue)
            {
                if (_distanceText != null) _distanceText.Text = $"{data.Distance.Value:F1} cm";
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
        
        if (_pitchThresholdValue != null)
            _pitchThresholdValue.Text = $"{pitchThreshold:F0}°";
        if (_distanceThresholdValue != null)
            _distanceThresholdValue.Text = $"{distanceThreshold:F0} cm";
        
        if (_wsClient != null)
        {
            await _wsClient.SetThresholdsAsync(pitchThreshold, distanceThreshold);
        }
    }
    
    private async Task StartCameraAsync()
    {
        try
        {
            if (_mediaCapture != null)
            {
                StopCamera();
            }
            
            _mediaCapture = new MediaCapture();
            
            var settings = new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video
            };
            
            await _mediaCapture.InitializeAsync(settings);
            
            // Find color video preview frame source
            var frameSource = _mediaCapture.FrameSources.FirstOrDefault(source =>
                source.Value.Info.MediaStreamType == MediaStreamType.VideoPreview &&
                source.Value.Info.SourceKind == MediaFrameSourceKind.Color).Value;
            
            if (frameSource == null)
            {
                // Fallback to any color video source
                frameSource = _mediaCapture.FrameSources.FirstOrDefault(source =>
                    source.Value.Info.SourceKind == MediaFrameSourceKind.Color).Value;
            }
            
            if (frameSource != null)
            {
                _frameReader = await _mediaCapture.CreateFrameReaderAsync(frameSource);
                _frameReader.FrameArrived += OnFrameArrived;
                await _frameReader.StartAsync();
            }
            
            System.Diagnostics.Debug.WriteLine("Camera started successfully");
        }
        catch (UnauthorizedAccessException)
        {
            if (_statusText != null)
                _statusText.Text = "Camera access denied. Please enable camera permissions.";
            System.Diagnostics.Debug.WriteLine("Camera access denied");
        }
        catch (Exception ex)
        {
            if (_statusText != null)
                _statusText.Text = $"Failed to start camera: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error starting camera: {ex}");
        }
    }
    
    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        var frame = sender.TryAcquireLatestFrame();
        if (frame?.VideoMediaFrame != null)
        {
            var softwareBitmap = frame.VideoMediaFrame.SoftwareBitmap;
            if (softwareBitmap != null)
            {
                if (softwareBitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8 ||
                    softwareBitmap.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                {
                    softwareBitmap = SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                }
                
                DispatcherQueue.TryEnqueue(async () =>
                {
                    if (_cameraPreview != null)
                    {
                        var bitmap = new SoftwareBitmapSource();
                        await bitmap.SetBitmapAsync(softwareBitmap);
                        _cameraPreview.Source = bitmap;
                    }
                    softwareBitmap?.Dispose();
                });
            }
        }
        frame?.Dispose();
    }
    
    private void StopCamera()
    {
        try
        {
            _previewCts?.Cancel();
            
            if (_frameReader != null)
            {
                _frameReader.FrameArrived -= OnFrameArrived;
                _frameReader.Dispose();
                _frameReader = null;
            }
            
            if (_mediaCapture != null)
            {
                _mediaCapture.Dispose();
                _mediaCapture = null;
            }
            
            if (_cameraPreview != null)
            {
                _cameraPreview.Source = null;
            }
            
            System.Diagnostics.Debug.WriteLine("Camera stopped");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping camera: {ex}");
        }
    }
}

