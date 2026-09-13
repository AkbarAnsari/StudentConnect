using StudentConnect.Application;

namespace StudentConnect.Infrastructure.Email;

// Dev-safe default when Smtp:Host isn't configured — logs instead of attempting a real
// SMTP connection, so the outbox/processor pipeline is fully exercisable without mail infra.
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("[No SMTP configured] Would send email to {ToEmail}: {Subject}\n{Body}", toEmail, subject, body);
        return Task.CompletedTask;
    }
}
