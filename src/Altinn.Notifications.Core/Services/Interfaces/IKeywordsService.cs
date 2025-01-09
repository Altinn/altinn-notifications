using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Services.Interfaces;

/// <summary>
/// Provides methods for handling keyword placeholders in <see cref="SmsRecipient"/> and <see cref="EmailRecipient"/>.
/// </summary>
public interface IKeywordsService
{
    /// <summary>
    /// Checks whether the specified string contains the placeholder keyword <c>$recipientName$</c>.
    /// </summary>
    /// <param name="value">The string to check for the placeholder keyword.</param>
    /// <returns><c>true</c> if the specified string contains the placeholder keyword <c>$recipientName$</c>; otherwise, <c>false</c>.</returns>
    bool ContainsRecipientNamePlaceholder(string? value);

    /// <summary>
    /// Checks whether the specified string contains the placeholder keyword <c>$recipientNumber$</c>.
    /// </summary>
    /// <param name="value">The string to check for the placeholder keyword.</param>
    /// <returns><c>true</c> if the specified string contains the placeholder keyword <c>$recipientNumber$</c>; otherwise, <c>false</c>.</returns>
    bool ContainsRecipientNumberPlaceholder(string? value);

    /// <summary>
    /// Replaces placeholder keywords in a collection of <see cref="SmsRecipient"/> with actual values.
    /// </summary>
    /// <param name="smsRecipients">The collection of <see cref="SmsRecipient"/> to process.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the collection of <see cref="SmsRecipient"/> with the placeholder keywords replaced by actual values.</returns>
    Task<IEnumerable<SmsRecipient>> ReplaceKeywordsAsync(IEnumerable<SmsRecipient> smsRecipients);

    /// <summary>
    /// Replaces placeholder keywords in a collection of <see cref="EmailRecipient"/> with actual values.
    /// </summary>
    /// <param name="emailRecipients">The collection of <see cref="EmailRecipient"/> to process.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the collection of <see cref="EmailRecipient"/> with the placeholder keywords replaced by actual values.</returns>
    Task<IEnumerable<EmailRecipient>> ReplaceKeywordsAsync(IEnumerable<EmailRecipient> emailRecipients);
}
