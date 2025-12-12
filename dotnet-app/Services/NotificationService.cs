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
            var soundPath = Path.Combine(AppContext.BaseDirectory, "Assets", "notification.mp3");
            
            if (File.Exists(soundPath))
            {
                _mediaPlayer ??= new MediaPlayer();
                _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri(soundPath));
                _mediaPlayer.Play();
            }
        }
        catch
        {
            // Silently fail if sound can't be played
        }
    }
}
