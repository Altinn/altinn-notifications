using Altinn.Notifications.Models;
using Swashbuckle.AspNetCore.Filters;

namespace Altinn.Notifications.Examples;

/// <summary>
/// Example provider for NotificationOrderRequestExt.
/// </summary>
public class NotificationOrderRequestExtExample : IExamplesProvider<NotificationOrderRequestExt>
{
    /// <summary>
            /// Gets the example instance with keywords.
            /// </summary>
            /// <returns>An example NotificationOrderRequestExt with keywords.</returns>
            public NotificationOrderRequestExt GetExamples()
    {
        return new NotificationOrderRequestExt()
        {
            SendersReference = "9A547448-FDF4-4E60-87AC-C2F652C8353C",
            EmailTemplate = new EmailTemplateExt()
            {
                Subject = "A test email from Altinn Notifications",
                Body = "A message to be sent immediately from an org.",
                ContentType = EmailContentTypeExt.Plain
            },
            SmsTemplate = new SmsTemplateExt()
            {
                Body = "Demo SMS content"
            },
            NotificationChannel = NotificationChannelExt.EmailPreferred,
            Recipients = new List<RecipientExt>()
            {
                new RecipientExt() { OrganizationNumber = "311000179" },
                new RecipientExt() { NationalIdentityNumber = "11876995923" }
            },
            ResourceId = "app_ttd_apps-test"
        };
    }
}

/// <summary>
/// Example provider for NotificationOrderRequestExt with keywords.
/// </summary>
public class NotificationOrderRequestExtKeywordsExample : IExamplesProvider<NotificationOrderRequestExt>
{
    /// <summary>
            /// Gets the example instance.
            /// </summary>
            /// <returns>An example NotificationOrderRequestExt.</returns>
            public NotificationOrderRequestExt GetExamples()
    {
        return new NotificationOrderRequestExt()
        {
            SendersReference = "518997F5-549C-4EC6-B5B8-040B7D73F725",
            EmailTemplate = new EmailTemplateExt()
            {
                Subject = "Important notification regarding your organization, $recipientName$",
                Body = "Dear $recipientName$, this is an official notification regarding your organization, identified by the organization number $recipientNumber$. Please take the necessary actions.",
                ContentType = EmailContentTypeExt.Plain
            },
            SmsTemplate = new SmsTemplateExt()
            {
                Body = "Dear $recipientName$, this is an official notification regarding your organization, identified by the organization number $recipientNumber$. Please take the necessary actions."
            },
            NotificationChannel = NotificationChannelExt.SmsPreferred,
            Recipients = new List<RecipientExt>()
            {
                new RecipientExt() { OrganizationNumber = "311000179" },
                new RecipientExt() { OrganizationNumber = "312508729" }
            },
            ResourceId = "app_ttd_apps-test"
        };
    }
}
