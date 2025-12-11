using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NeatBack.Models;
using NeatBack.Services;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;

namespace NeatBack.Views;

public sealed partial class MainPage : Page
{
    // Service fields
    private WebSocketClient? _wsClient;
    private NotificationService? _notificationService;
    private DateTime _badPostureStart;
    private bool _inBadPosture = false;
    private bool _isMonitoring = false;
    
    public MainPage()
    {
        InitializeComponent();
        _wsClient = new WebSocketClient();
        _notificationService = new NotificationService();
        _wsClient.DataReceived += OnPostureDataReceived;
    }
    
    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_wsClient != null)
            {
                if (!_isMonitoring)
                {
                    // Start monitoring
                    await _wsClient.ConnectAsync();
                    StatusText.Text = "Monitoring...";
                    StartButton.Content = "Stop Monitoring";
                    _isMonitoring = true;
                }
                else
                {
                    // Stop monitoring
                    await _wsClient.DisconnectAsync();
                    StatusText.Text = "Not monitoring";
                    AngleText.Text = "Neck Angle: --";
                    StartButton.Content = "Start Monitoring";
                    _isMonitoring = false;
                    _inBadPosture = false;
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }
    
    private void OnPostureDataReceived(object? sender, PostureData data)
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            AngleText.Text = $"Neck Angle: {data.neck_angle}°";
            StatusText.Text = data.is_good ? "Good Posture ✓" : "Bad Posture ✗";
            
            // Display webcam frame if available
            if (!string.IsNullOrEmpty(data.frame))
            {
                try
                {
                    var imageBytes = Convert.FromBase64String(data.frame);
                    using var stream = new MemoryStream(imageBytes);
                    var bitmap = new BitmapImage();
                    await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
                    WebcamImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error displaying frame: {ex.Message}");
                }
            }
            
            // Track bad posture duration
            if (!data.is_good)
            {
                if (!_inBadPosture)
                {
                    _badPostureStart = DateTime.Now;
                    _inBadPosture = true;
                }
                else
                {
                    var duration = (DateTime.Now - _badPostureStart).TotalSeconds;
                    if (duration > 30)
                    {
                        _notificationService?.ShowAlert("Fix your posture!");
                    }
                }
            }
            else
            {
                _inBadPosture = false;
            }
        });
    }
}
