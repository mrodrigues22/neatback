using Microsoft.Toolkit.Uwp.Notifications;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Slouti.Services;

public class NotificationService : IDisposable
{
    private DateTime _lastNotification = DateTime.MinValue;
    private MediaPlayer? _mediaPlayer;
    private bool _disposed = false;
    
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
            // Dispose previous player if it exists to avoid state issues
            _mediaPlayer?.Dispose();
            
            Uri soundUri;
            
            // Try local file path first (for debug mode)
            var localPath = Path.Combine(AppContext.BaseDirectory, "Assets", "notification.mp3");
            if (File.Exists(localPath))
            {
                soundUri = new Uri(localPath, UriKind.Absolute);
            }
            else
            {
                // Fall back to ms-appx URI for packaged apps
                soundUri = new Uri("ms-appx:///Assets/notification.mp3");
            }
            
            // Create a new MediaPlayer instance each time for reliability
            _mediaPlayer = new MediaPlayer
            {
                AutoPlay = false,
                Volume = 1.0
            };
            
            _mediaPlayer.Source = MediaSource.CreateFromUri(soundUri);
            _mediaPlayer.Play();
        }
        catch (Exception ex)
        {
            // Failed to play notification sound
        }
    }
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _mediaPlayer?.Dispose();
            _mediaPlayer = null;
            _disposed = true;
        }
    }
}
