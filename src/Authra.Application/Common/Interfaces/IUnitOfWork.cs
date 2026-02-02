namespace Authra.Application.Common.Interfaces;

/// <summary>
/// Unit of Work pattern for coordinating database transactions.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
