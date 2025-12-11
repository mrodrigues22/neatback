using Microsoft.Toolkit.Uwp.Notifications;

namespace NeatBack.Services;

public class NotificationService
{
    private DateTime _lastNotification = DateTime.MinValue;
    
    public void ShowAlert(string message)
    {
        // Only show notification every 30 seconds
        if ((DateTime.Now - _lastNotification).TotalSeconds < 30)
            return;
        
        new ToastContentBuilder()
            .AddText("Posture Alert")
            .AddText(message)
            .Show();
        
        _lastNotification = DateTime.Now;
    }
}
