namespace Altinn.Notifications.Tools.DlqManager.Services.Queues;

/// <summary>
/// Operations available for the <c>altinn.notifications.sms.send</c> DLQ.
/// </summary>
public interface ISmsSendQueueService
{
    /// <summary>
    /// Displays the queue sub-menu and handles user input in a loop until the user
    /// chooses to go back to the main menu. Ctrl+C exits the process immediately.
    /// </summary>
    Task RunMenuAsync();
}
