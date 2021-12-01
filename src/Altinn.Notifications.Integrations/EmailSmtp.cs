using System.Net.Mail;


using Altinn.Notifications.Core;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations
{
    /// <summary>
    /// Smtp client implementation of IEmail
    /// </summary>≈
    public class EmailSmtp : IEmail
    {
        private readonly SmtpSettings _smtpSettings;

        public EmailSmtp(IOptions<SmtpSettings> smtpSettings)
        {
            _smtpSettings = smtpSettings.Value;
        }

        /// <inheritdoc/>
        public async Task<bool> SendEmailAsync(string address, string subject, string body, CancellationToken cancellationToken)
        {
            MailMessage msg = new MailMessage();
            msg.From = new MailAddress(_smtpSettings.Sender);
            msg.To.Add(new MailAddress(address));
            msg.Subject = subject;
            msg.Body = body;
            SmtpClient client = new SmtpClient(_smtpSettings.Host, _smtpSettings.Port);
            client.UseDefaultCredentials = true;

            await client.SendMailAsync(msg, cancellationToken);

            return true;
        }

    }
}