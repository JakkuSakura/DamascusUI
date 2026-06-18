using Foundation;
using UserNotifications;

namespace DamascusUI;

internal static class MacNotification
{
    private static readonly object Gate = new();
    private static NotificationDelegate? _delegate;
    private static readonly Dictionary<string, Action> PendingClicks = new();
    private static bool _initialized;
    private const double TriggerDelaySeconds = 1.0;

    public static void TryShow(string title, string body, Action onClick)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        EnsureInitialized();

        var center = UNUserNotificationCenter.Current;
        var identifier = Guid.NewGuid().ToString("N");
        center.GetNotificationSettings(settings =>
        {
            switch (settings.AuthorizationStatus)
            {
                case UNAuthorizationStatus.Authorized:
                case UNAuthorizationStatus.Provisional:
                    ScheduleNotification(center, identifier, title, body, onClick);
                    break;
                case UNAuthorizationStatus.NotDetermined:
                    center.RequestAuthorization(
                        UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge,
                        (granted, error) =>
                        {
                            if (error is not null || !granted)
                            {
                                return;
                            }

                            ScheduleNotification(center, identifier, title, body, onClick);
                        });
                    break;
                default:
                    lock (Gate)
                    {
                        PendingClicks.Remove(identifier);
                    }
                    break;
            }
        });
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            _delegate = new NotificationDelegate();
            UNUserNotificationCenter.Current.Delegate = _delegate;
            _initialized = true;
        }
    }

    private static void ScheduleNotification(
        UNUserNotificationCenter center,
        string identifier,
        string title,
        string body,
        Action onClick)
    {
        // Check focus on a background thread; this is diagnostic-only and must not
        // block the main thread (GetNotificationSettings callback runs on main queue).
        _ = Task.Run(() =>
        {
            if (MacFocus.TryIsFocusLikelyActive() == true)
                Console.Error.WriteLine(
                    "[MacNotification] warning: Focus/Do Not Disturb appears active; macOS may mute or delay the notification banner.");
        });

        var content = new UNMutableNotificationContent
        {
            Title = title,
            Body = body,
            Sound = UNNotificationSound.Default
        };

        lock (Gate)
        {
            PendingClicks[identifier] = onClick;
        }

        var trigger = UNTimeIntervalNotificationTrigger.CreateTrigger(TriggerDelaySeconds, false);
        var request = UNNotificationRequest.FromIdentifier(identifier, content, trigger);
        center.AddNotificationRequest(request, addError =>
        {
            if (addError is null)
            {
                return;
            }

            lock (Gate)
            {
                PendingClicks.Remove(identifier);
            }
        });
    }

    private sealed class NotificationDelegate : UNUserNotificationCenterDelegate
    {
        public override void DidReceiveNotificationResponse(
            UNUserNotificationCenter center,
            UNNotificationResponse response,
            Action completionHandler)
        {
            Action? onClick = null;
            lock (Gate)
            {
                if (PendingClicks.TryGetValue(response.Notification.Request.Identifier, out var callback))
                {
                    onClick = callback;
                    PendingClicks.Remove(response.Notification.Request.Identifier);
                }
            }

            onClick?.Invoke();
            completionHandler();
        }

        public override void WillPresentNotification(
            UNUserNotificationCenter center,
            UNNotification notification,
            Action<UNNotificationPresentationOptions> completionHandler)
        {
            completionHandler(
                UNNotificationPresentationOptions.List |
                UNNotificationPresentationOptions.Banner |
                UNNotificationPresentationOptions.Sound);
        }
    }
}
