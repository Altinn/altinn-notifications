using Altinn.Notifications.Core.Models;

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
        bool ContainsRecipientNamePlaceholder(string value);

        /// <summary>
        /// Checks whether the specified string contains the placeholder keyword $recipientNumber$.
        /// </summary>
        /// <param name="value">The string to check.</param>
        /// <returns><c>true</c> if the specified string contains the placeholder keyword $recipientNumber$; otherwise, <c>false</c>.</returns>
        bool ContainsRecipientNumberPlaceholder(string value);

        /// <summary>
        /// Replaces placeholder keywords in a collection of <seealso cref="Sms"/> with actual values.
        /// </summary>
        /// <param name="smsList">The collection of <seealso cref="Sms"/> to process.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the collection of <seealso cref="Sms"/> with replaced keywords.</returns>
        Task<List<Sms>> ReplaceKeywordsAsync(List<Sms> smsList);

        /// <summary>
        /// Replaces placeholder keywords in a collection of <seealso cref="Email"/> with actual values.
        /// </summary>
        /// <param name="emailList">The collection of <seealso cref="Email"/> to process.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the collection of <seealso cref="Email"/> with replaced keywords.</returns>
        Task<List<Email>> ReplaceKeywordsAsync(List<Email> emailList);
    }
}
