namespace StudentConnect.Application;

// Actually delivers one email — implementations shouldn't know or care that the message
// came from the outbox table; EmailOutboxProcessor owns the retry/scheduling logic.
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string body, CancellationToken ct = default);
}
