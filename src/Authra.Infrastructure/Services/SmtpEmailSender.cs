using Authra.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Configuration options for SMTP email sending.
/// </summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    /// <summary>
    /// SMTP server hostname.
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// SMTP server port.
    /// </summary>
    public int Port { get; set; } = 1025;

    /// <summary>
    /// Username for SMTP authentication (optional).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Password for SMTP authentication (optional).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Whether to use SSL/TLS.
    /// </summary>
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Whether to use STARTTLS.
    /// </summary>
    public bool UseStartTls { get; set; } = false;

    /// <summary>
    /// Default sender email address.
    /// </summary>
    public string FromAddress { get; set; } = "noreply@authra.io";

    /// <summary>
    /// Default sender name.
    /// </summary>
    public string FromName { get; set; } = "Authra";
}

/// <summary>
/// SMTP-based email sender using MailKit.
/// Works with Mailpit (dev), Resend SMTP (prod), or any SMTP server.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var email = new MimeMessage();
        email.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        email.To.Add(MailboxAddress.Parse(message.To));
        email.Subject = message.Subject;

        var builder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody
        };

        if (!string.IsNullOrEmpty(message.TextBody))
        {
            builder.TextBody = message.TextBody;
        }

        email.Body = builder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();

            var secureSocketOptions = SecureSocketOptions.None;
            if (_options.UseSsl)
            {
                secureSocketOptions = SecureSocketOptions.SslOnConnect;
            }
            else if (_options.UseStartTls)
            {
                secureSocketOptions = SecureSocketOptions.StartTls;
            }

            await client.ConnectAsync(_options.Host, _options.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            }

            await client.SendAsync(email, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent to {To}: {Subject}", message.To, message.Subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", message.To, message.Subject);
            throw;
        }
    }
}

/// <summary>
/// In-memory email sender for testing.
/// </summary>
public class InMemoryEmailSender : IEmailSender
{
    private readonly List<EmailMessage> _sentEmails = [];
    private readonly object _lock = new();

    public IReadOnlyList<EmailMessage> SentEmails
    {
        get
        {
            lock (_lock)
            {
                return _sentEmails.ToList();
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _sentEmails.Add(message);
        }
        return Task.CompletedTask;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _sentEmails.Clear();
        }
    }
}
