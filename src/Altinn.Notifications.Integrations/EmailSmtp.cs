using System.Net.Mail;


using Altinn.Notifications.Core;
using Altinn.Notifications.Integrations.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Notifications.Integrations
{
    /// <summary>
    /// Smtp client implementation of IEmail
    /// </summary>
    public class EmailSmtp : IEmail
    {
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<IEmail> _logger;

        public EmailSmtp(IOptions<SmtpSettings> smtpSettings, ILogger<IEmail> logger)
        {
            _smtpSettings = smtpSettings.Value;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<bool> SendEmailAsync(string address, string subject, string body, CancellationToken cancellationToken)
        {
             _logger.LogError($"//EmailSmtp // Sender {_smtpSettings.Sender}");
            _logger.LogError($"//EmailSmtp // Host {_smtpSettings.Host}");
            _logger.LogError($"//EmailSmtp // Port {_smtpSettings.Port}");
            _logger.LogError($"//EmailSmtp // Address {address}");
            _logger.LogError($"//EmailSmtp // Subject {subject}");
            _logger.LogError($"//EmailSmtp // Body {body}");

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