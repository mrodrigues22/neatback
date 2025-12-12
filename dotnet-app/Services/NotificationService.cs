using Microsoft.Toolkit.Uwp.Notifications;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Slouti.Services;

public class NotificationService
{
    private DateTime _lastNotification = DateTime.MinValue;
    private MediaPlayer? _mediaPlayer;
    
    public bool IsMuted { get; set; } = false;
    
    public void ShowAlert(string message)
    {   
        // Only show notification every 30 seconds
        if ((DateTime.Now - _lastNotification).TotalSeconds < 30)
            return;
        
        new ToastContentBuilder()
            .AddText("Posture Alert")
            .AddText(message)
            .Show();
            
        if (!IsMuted)
            PlayNotificationSound();
        
        _lastNotification = DateTime.Now;
    }
    
    private void PlayNotificationSound()
    {
        try
        {
            // Use ms-appx URI for packaged apps - this works in both debug and installed scenarios
            var soundUri = new Uri("ms-appx:///Assets/notification.mp3");
            
            _mediaPlayer ??= new MediaPlayer();
            _mediaPlayer.Source = MediaSource.CreateFromUri(soundUri);
            _mediaPlayer.Play();
        }
        catch (Exception ex)
        {
            // Log the error so we can see what's happening
            System.Diagnostics.Debug.WriteLine($"Failed to play notification sound: {ex.Message}");
        }
    }
}
