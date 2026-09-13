using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Email;

// Real SMTP delivery — used only when Smtp:Host is configured (see Program.cs); otherwise
// LoggingEmailSender is registered instead so local dev works without a real mail server.
public class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        using var client = new SmtpClient(_options.Host, _options.Port) { EnableSsl = _options.EnableSsl };
        if (!string.IsNullOrEmpty(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        using var message = new MailMessage(new MailAddress(_options.FromAddress, _options.FromName), new MailAddress(toEmail))
        {
            Subject = subject,
            Body = body,
        };

        logger.LogInformation("Sending email to {ToEmail}: {Subject}", toEmail, subject);
        await client.SendMailAsync(message, ct);
    }
}
