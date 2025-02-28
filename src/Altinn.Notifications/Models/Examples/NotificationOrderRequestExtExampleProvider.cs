using Swashbuckle.AspNetCore.Filters;

namespace Altinn.Notifications.Models.Examples;

/// <summary>
/// Provides example
/// </summary>
public class NotificationOrderRequestExtExampleProvider : IExamplesProvider<NotificationOrderRequestExt>
{
    /// <inheritdoc/>
    public NotificationOrderRequestExt GetExamples()
    {
        return new NotificationOrderRequestExt
        {
            ConditionEndpoint = new Uri("https://vg.no"),

            EmailTemplate = new EmailTemplateExt
            {
                Body = "is ready",
                ContentType = EmailContentTypeExt.Plain,
                FromAddress = "foo@barman.com",
                Subject = "It's great"
            },
            IgnoreReservation = false,
            NotificationChannel = NotificationChannelExt.Email,
            Recipients =
            [
                new RecipientExt 
                {
                    EmailAddress = "foo@bar.com",
                    IsReserved = false,
                    MobileNumber = "+4712346789",
                    NationalIdentityNumber = "01010111111",
                    OrganizationNumber = "123"
                }
            ],
            SendersReference = "snowman-carrot",
            RequestedSendTime = DateTime.Now,
            ResourceId = "1234-12-1234",
            SmsTemplate = new SmsTemplateExt
            {
                Body = "En bra melding",
                SenderNumber = "+4712345678"
            }
        };
    }
}
