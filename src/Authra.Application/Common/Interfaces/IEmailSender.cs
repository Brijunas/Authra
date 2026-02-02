namespace Authra.Application.Common.Interfaces;

/// <summary>
/// Email message to send.
/// </summary>
public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? TextBody = null);

/// <summary>
/// Abstraction for sending emails.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
