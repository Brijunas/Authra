namespace Authra.Application.Common.Interfaces;

/// <summary>
/// Abstraction for getting current time, enabling testability.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
