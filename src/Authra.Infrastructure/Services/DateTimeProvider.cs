using Authra.Application.Common.Interfaces;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Production implementation of IDateTimeProvider using system time.
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
