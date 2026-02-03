using System.Net;

namespace Authra.Domain.Entities;

/// <summary>
/// Ownership transfer audit trail for tenant ownership changes.
/// MVP: Immediate transfer. v1.1: Two-phase workflow with acceptance.
/// Tenant-scoped entity with RLS support.
/// </summary>
public class OwnershipTransfer : TenantEntity
{
    /// <summary>
    /// Member who initiated the transfer (current owner).
    /// ON DELETE RESTRICT - preserve audit trail.
    /// </summary>
    public Guid FromMemberId { get; private set; }
    public TenantMember FromMember { get; private set; } = null!;

    /// <summary>
    /// Member receiving ownership.
    /// ON DELETE RESTRICT - preserve audit trail.
    /// </summary>
    public Guid ToMemberId { get; private set; }
    public TenantMember ToMember { get; private set; } = null!;

    /// <summary>
    /// Transfer status: pending, completed, cancelled, expired.
    /// </summary>
    public string Status { get; private set; } = "pending";

    /// <summary>
    /// When the transfer was initiated.
    /// </summary>
    public DateTimeOffset InitiatedAt { get; private set; }

    /// <summary>
    /// When the transfer was completed. NULL if not completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>
    /// Member who completed the transfer (may differ from initiator in v1.1).
    /// ON DELETE SET NULL - keep history.
    /// </summary>
    public Guid? CompletedByMemberId { get; private set; }
    public TenantMember? CompletedByMember { get; private set; }

    /// <summary>
    /// When the transfer was cancelled. NULL if not cancelled.
    /// </summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    /// <summary>
    /// Reason for cancellation.
    /// </summary>
    public string? CancelReason { get; private set; }

    /// <summary>
    /// IP address that initiated the transfer.
    /// </summary>
    public IPAddress? InitiatedByIp { get; private set; }

    /// <summary>
    /// IP address that completed the transfer.
    /// </summary>
    public IPAddress? CompletedByIp { get; private set; }

    /// <summary>
    /// v1.1 ready: When the transfer expires (two-phase workflow).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// v1.1 ready: When the recipient accepted the transfer.
    /// </summary>
    public DateTimeOffset? AcceptedAt { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private OwnershipTransfer()
    {
        // EF Core constructor
    }

    /// <summary>
    /// Creates an immediate ownership transfer (MVP flow).
    /// </summary>
    public static OwnershipTransfer CreateImmediate(
        Guid tenantId,
        Guid fromMemberId,
        Guid toMemberId,
        Guid completedByMemberId,
        IPAddress? initiatedByIp = null,
        IPAddress? completedByIp = null)
    {
        var now = DateTimeOffset.UtcNow;

        return new OwnershipTransfer
        {
            TenantId = tenantId,
            FromMemberId = fromMemberId,
            ToMemberId = toMemberId,
            Status = "completed",
            InitiatedAt = now,
            CompletedAt = now,
            CompletedByMemberId = completedByMemberId,
            InitiatedByIp = initiatedByIp,
            CompletedByIp = completedByIp
        };
    }

    /// <summary>
    /// Creates a pending ownership transfer (v1.1 two-phase flow).
    /// </summary>
    public static OwnershipTransfer CreatePending(
        Guid tenantId,
        Guid fromMemberId,
        Guid toMemberId,
        DateTimeOffset expiresAt,
        IPAddress? initiatedByIp = null)
    {
        return new OwnershipTransfer
        {
            TenantId = tenantId,
            FromMemberId = fromMemberId,
            ToMemberId = toMemberId,
            Status = "pending",
            InitiatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
            InitiatedByIp = initiatedByIp
        };
    }

    /// <summary>
    /// Accepts the transfer (v1.1 two-phase flow).
    /// </summary>
    public void Accept(DateTimeOffset acceptedAt)
    {
        if (Status != "pending")
            throw new InvalidOperationException($"Cannot accept transfer in '{Status}' status.");

        AcceptedAt = acceptedAt;
    }

    /// <summary>
    /// Completes the transfer.
    /// </summary>
    public void Complete(
        Guid completedByMemberId,
        DateTimeOffset completedAt,
        IPAddress? completedByIp = null)
    {
        if (Status != "pending")
            throw new InvalidOperationException($"Cannot complete transfer in '{Status}' status.");

        Status = "completed";
        CompletedAt = completedAt;
        CompletedByMemberId = completedByMemberId;
        CompletedByIp = completedByIp;
    }

    /// <summary>
    /// Cancels the transfer.
    /// </summary>
    public void Cancel(string reason, DateTimeOffset cancelledAt)
    {
        if (Status != "pending")
            throw new InvalidOperationException($"Cannot cancel transfer in '{Status}' status.");

        Status = "cancelled";
        CancelledAt = cancelledAt;
        CancelReason = reason;
    }

    /// <summary>
    /// Marks the transfer as expired.
    /// </summary>
    public void Expire()
    {
        if (Status != "pending")
            throw new InvalidOperationException($"Cannot expire transfer in '{Status}' status.");

        Status = "expired";
    }

    public bool IsPending => Status == "pending";
    public bool IsCompleted => Status == "completed";
    public bool IsCancelled => Status == "cancelled";
    public bool IsExpired => Status == "expired";

    public bool IsExpiredAt(DateTimeOffset now) =>
        ExpiresAt.HasValue && now >= ExpiresAt.Value && Status == "pending";
}
