using System.Composition;
using Microsoft.CodeAnalysis.ExternalAccess.OmniSharp.Notification;

namespace OmniSharp
{
    [Shared]
    [Export(typeof(IOmniSharpNotificationService))]
    internal class NotificationWorkspaceService : IOmniSharpNotificationService
    {
        [ImportingConstructor]
        public NotificationWorkspaceService()
        {
        }

        public bool ConfirmMessageBox(string message, string title = null, OmniSharpNotificationSeverity severity = OmniSharpNotificationSeverity.Warning)
        {
            return true;
        }

        public void SendNotification(string message, string title = null, OmniSharpNotificationSeverity severity = OmniSharpNotificationSeverity.Warning)
        {
            return;
        }
    }
}
