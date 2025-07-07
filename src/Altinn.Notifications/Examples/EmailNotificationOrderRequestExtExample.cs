using Altinn.Notifications.Models;
using Altinn.Notifications.Models.Email;
using Swashbuckle.AspNetCore.Filters;

namespace Altinn.Notifications.Examples;

/// <summary>
/// Example provider for EmailNotificationOrderRequestExt.
/// </summary>
public class EmailNotificationOrderRequestExtExample : IExamplesProvider<EmailNotificationOrderRequestExt>
{
    /// <summary>
            /// Gets the example instance.
            /// </summary>
            /// <returns>An example EmailNotificationOrderRequestExt.</returns>
            public EmailNotificationOrderRequestExt GetExamples()
    {
        return new EmailNotificationOrderRequestExt()
        {
            SendersReference = "ref-2023-12-01",
            Subject = "A test email from Altinn Notifications",
            Body = "A message to be sent immediately from an org.",
            ContentType = EmailContentTypeExt.Plain,
            Recipients =
            [
                new RecipientExt() { EmailAddress = "testuser@altinn.no" },
                new RecipientExt() { NationalIdentityNumber = "11876995923" },
                new RecipientExt() { OrganizationNumber = "311000179" }
            ]
        };
    }
}

/// <summary>
/// Example provider for EmailNotificationOrderRequestExt with keywords.
/// </summary>
public class EmailNotificationOrderRequestExtKeywordsExample : IExamplesProvider<EmailNotificationOrderRequestExt>
{
    /// <summary>
            /// Gets the example instance with keywords.
            /// </summary>
            /// <returns>An example EmailNotificationOrderRequestExt with keywords.</returns>
            public EmailNotificationOrderRequestExt GetExamples()
    {
        return new EmailNotificationOrderRequestExt()
        {
            SendersReference = "ref-2023-12-02",
            Subject = "Important notification regarding your organization, $recipientName$",
            Body = "Dear $recipientName$, this is an official notification regarding your organization, identified by the organization number $recipientNumber$. Please take the necessary actions.",
            ContentType = EmailContentTypeExt.Plain,
            Recipients = [new RecipientExt() { OrganizationNumber = "311000179" }]
        };
    }
}
