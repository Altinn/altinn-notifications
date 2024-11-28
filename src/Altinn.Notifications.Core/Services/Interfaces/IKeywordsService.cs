using Altinn.Notifications.Core.Models;
using Altinn.Notifications.Core.Models.Recipients;

namespace Altinn.Notifications.Core.Services.Interfaces
{
    /// <summary>
    /// Provides methods for handling keyword placeholders in collections of <seealso cref="Sms"/> or <seealso cref="Email"/>.
    /// </summary>
    public interface IKeywordsService
    {
        /// <summary>
        /// Checks whether the specified string contains the placeholder keyword $recipientName$.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <returns><c>true</c> if the specified string contains the placeholder keyword $recipientName$; otherwise, <c>false</c>.</returns>
        bool ContainsRecipientNamePlaceholder(string? value);

        /// <summary>
        /// Checks whether the specified string contains the placeholder keyword $recipientNumber$.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <returns><c>true</c> if the specified string contains the placeholder keyword $recipientNumber$; otherwise, <c>false</c>.</returns>
        bool ContainsRecipientNumberPlaceholder(string? value);

        /// <summary>
        /// Replaces placeholder keywords in an <seealso cref="SmsRecipient"/> with actual values.
        /// </summary>
        /// <param name="smsRecipient">The <seealso cref="SmsRecipient"/> to process.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <seealso cref="SmsRecipient"/> with actual values.</returns>
        Task<SmsRecipient> ReplaceKeywordsAsync(SmsRecipient smsRecipient);

        /// <summary>
        /// Replaces placeholder keywords in an <seealso cref="EmailRecipient"/> with actual values.
        /// </summary>
        /// <param name="emailRecipient">The <seealso cref="EmailRecipient"/> to process.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the <seealso cref="EmailRecipient"/> with actual values.</returns>
        Task<EmailRecipient> ReplaceKeywordsAsync(EmailRecipient emailRecipient);
    }
}
