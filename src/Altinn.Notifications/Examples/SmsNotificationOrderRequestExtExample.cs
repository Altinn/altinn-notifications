using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Sms;
using Swashbuckle.AspNetCore.Filters;

namespace Altinn.Notifications.Examples;

/// <summary>
/// Example provider for SmsNotificationOrderRequestExt.
/// </summary>
public class SmsNotificationOrderRequestExtExample : IExamplesProvider<SmsNotificationOrderRequestExt>
{
    /// <summary>
            /// Gets the example instance.
            /// </summary>
            /// <returns>An example SmsNotificationOrderRequestExt.</returns>
            public SmsNotificationOrderRequestExt GetExamples()
    {
        return new SmsNotificationOrderRequestExt()
        {
            SendersReference = "ref-2024-01-01",
            Body = "A text message to be sent immediately from an org.",
            Recipients = new List<RecipientExt>()
            {
                new RecipientExt() { MobileNumber = "+4799999999" },
                new RecipientExt() { NationalIdentityNumber = "11876995923" },
                new RecipientExt() { OrganizationNumber = "311000179" }
            }
        };
    }
}

/// <summary>
/// Example provider for SmsNotificationOrderRequestExt with keywords.
/// </summary>
public class SmsNotificationOrderRequestExtKeywordsExample : IExamplesProvider<SmsNotificationOrderRequestExt>
{
    /// <summary>
            /// Gets the example instance with keywords.
            /// </summary>
            /// <returns>An example SmsNotificationOrderRequestExt with keywords.</returns>
            public SmsNotificationOrderRequestExt GetExamples()
    {
        return new SmsNotificationOrderRequestExt()
        {
            SendersReference = "ref-2024-02-01",
            Body = "Dear $recipientName$, this is an official notification regarding your organization, identified by the organization number $recipientNumber$. Please take the necessary actions.",
            Recipients = new List<RecipientExt>()
            {
                new RecipientExt() { OrganizationNumber = "311000179" }
            }
        };
    }
}
