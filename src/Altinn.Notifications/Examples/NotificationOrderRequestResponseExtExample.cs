using Altinn.Notifications.Models;
using Swashbuckle.AspNetCore.Filters;

namespace Altinn.Notifications.Examples;

/// <summary>
/// Example provider for NotificationOrderRequestResponseExt.
/// </summary>
public class NotificationOrderRequestResponseExtExample : IExamplesProvider<NotificationOrderRequestResponseExt>
{
    /// <summary>
            /// Gets the example instance.
            /// </summary>
            /// <returns>An example NotificationOrderRequestResponseExt.</returns>
            public NotificationOrderRequestResponseExt GetExamples()
    {
        return new NotificationOrderRequestResponseExt()
        {
            OrderId = Guid.NewGuid(),
            RecipientLookup = new RecipientLookupResultExt()
            {
                Status = RecipientLookupStatusExt.PartialSuccess,
                IsReserved = new List<string>() { "11876995923" },
                MissingContact = new List<string>()
            }
        };
    }
}
