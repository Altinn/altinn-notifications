using System.Net.Mail;


using Altinn.Notifications.Core;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations
{
    /// <summary>
    /// Smtp client implementation of IEmail
    /// </summary>≈
    public class EmailSmtp : IEmail
    {
        private readonly SmtpConfiguration _smtpConfiguration;

        public EmailSmtp(IOptions<SmtpConfiguration> smtpConfiguration)
        {
            _smtpConfiguration = smtpConfiguration.Value;
        }

        /// <inheritdoc/>
        public async Task<bool> SendEmailAsync(string address, string subject, string body, CancellationToken cancellationToken)
        {
            MailMessage msg = new MailMessage();
            msg.From = new MailAddress(_smtpConfiguration.Sender);
            msg.To.Add(new MailAddress(address));
            msg.Subject = subject;
            msg.Body = body;
            SmtpClient client = new SmtpClient(_smtpConfiguration.Host, _smtpConfiguration.Port);
            client.UseDefaultCredentials = true;
            await client.SendMailAsync(msg, cancellationToken);
            return true;
        }

    }
}