namespace Altinn.Notifications.Shared.Commands;

/// <summary>
/// Represents a command to send an email notification from the Notifications API to the Email service.
/// </summary>
public sealed record SendEmailCommand : EmailCommandBase;
