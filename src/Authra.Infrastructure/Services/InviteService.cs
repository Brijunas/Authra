using System.Security.Cryptography;
using Authra.Application.Common;
using Authra.Application.Common.DTOs;
using Authra.Application.Common.Interfaces;
using Authra.Application.Tenants;
using Authra.Application.Tenants.DTOs;
using Authra.Domain.Entities;
using Authra.Domain.Exceptions;
using Authra.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Authra.Infrastructure.Services;

/// <summary>
/// Invite service implementation for tenant invitations.
/// </summary>
public class InviteService : IInviteService
{
    private readonly AppDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IDateTimeProvider _dateTimeProvider;

    private static readonly TimeSpan InviteExpiration = TimeSpan.FromDays(7);

    public InviteService(
        AppDbContext context,
        IEmailSender emailSender,
        IDateTimeProvider dateTimeProvider)
    {
        _context = context;
        _emailSender = emailSender;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<InviteResponse> CreateInviteAsync(Guid tenantId, Guid invitedByMemberId, CreateInviteRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        // Check if user is already a member
        var existingMember = await _context.TenantMembers
            .Include(m => m.User)
                .ThenInclude(u => u.Identifiers)
            .Where(m => m.TenantId == tenantId && m.Status != "removed")
            .Where(m => m.User.Identifiers.Any(i => i.Type == "email" && i.ValueNormalized == normalizedEmail))
            .FirstOrDefaultAsync(cancellationToken);

        if (existingMember != null)
        {
            throw new ConflictException("User is already a member of this tenant");
        }

        // Check for pending invite to same email
        var existingInvite = await _context.Invites
            .Where(i => i.TenantId == tenantId && i.Email == normalizedEmail && i.Status == "pending")
            .FirstOrDefaultAsync(cancellationToken);

        if (existingInvite != null && !existingInvite.IsExpired)
        {
            throw new ConflictException("An invitation has already been sent to this email");
        }

        // Cancel expired invite if exists
        if (existingInvite != null && existingInvite.IsExpired)
        {
            existingInvite.MarkExpired();
        }

        // Validate role IDs if provided
        var roleIds = new List<Guid>();
        if (request.RoleIds != null && request.RoleIds.Count > 0)
        {
            foreach (var roleIdStr in request.RoleIds)
            {
                var roleId = IdPrefix.DecodeRole(roleIdStr);
                var roleExists = await _context.Roles
                    .AnyAsync(r => r.Id == roleId && r.TenantId == tenantId, cancellationToken);

                if (!roleExists)
                {
                    throw new NotFoundException("Role", roleId);
                }

                roleIds.Add(roleId);
            }
        }
        else
        {
            // Assign default member role if no roles specified
            var defaultRole = await _context.Roles
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.IsDefault, cancellationToken);

            if (defaultRole != null)
            {
                roleIds.Add(defaultRole.Id);
            }
        }

        // Generate invite token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        // Get tenant and inviter info
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", tenantId);

        var inviter = await _context.TenantMembers
            .Include(m => m.User)
                .ThenInclude(u => u.Identifiers)
            .FirstOrDefaultAsync(m => m.Id == invitedByMemberId, cancellationToken)
            ?? throw new NotFoundException("Member", invitedByMemberId);

        var inviterEmail = inviter.User.Identifiers.FirstOrDefault(i => i.Type == "email")?.ValueNormalized;

        // Create invite
        var now = _dateTimeProvider.UtcNow;
        var invite = tenant.CreateInvite(
            normalizedEmail,
            invitedByMemberId,
            now.Add(InviteExpiration),
            token,
            roleIds);

        await _context.SaveChangesAsync(cancellationToken);

        // Send invitation email
        await _emailSender.SendAsync(new EmailMessage(
            To: normalizedEmail,
            Subject: $"You've been invited to join {tenant.Name} on Authra",
            HtmlBody: $@"
                <h2>You've been invited!</h2>
                <p>{inviterEmail} has invited you to join <strong>{tenant.Name}</strong> on Authra.</p>
                <p><a href=""https://authra.io/invites/{token}"">Accept Invitation</a></p>
                <p>This invitation expires in 7 days.</p>
                <p>If you don't have an Authra account, you'll be prompted to create one.</p>
            ",
            TextBody: $@"
You've been invited!

{inviterEmail} has invited you to join {tenant.Name} on Authra.

Accept your invitation: https://authra.io/invites/{token}

This invitation expires in 7 days.
If you don't have an Authra account, you'll be prompted to create one.
            "),
            cancellationToken);

        return MapToResponse(invite, inviterEmail);
    }

    public async Task<PagedResponse<InviteResponse>> ListInvitesAsync(Guid tenantId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(pagination.Limit, 1, 100);

        var baseQuery = _context.Invites
            .Include(i => i.InvitedByMember)
                .ThenInclude(m => m.User)
                    .ThenInclude(u => u.Identifiers)
            .Where(i => i.TenantId == tenantId && i.Status == "pending");

        // Apply cursor filter
        if (!string.IsNullOrEmpty(pagination.Cursor))
        {
            var cursorId = DecodeCursor(pagination.Cursor);
            baseQuery = baseQuery.Where(i => i.Id.CompareTo(cursorId) > 0);
        }

        var invites = await baseQuery
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = invites.Count > limit;
        var items = invites.Take(limit).Select(i =>
        {
            var inviterEmail = i.InvitedByMember.User.Identifiers
                .FirstOrDefault(id => id.Type == "email")?.ValueNormalized;
            return MapToResponse(i, inviterEmail);
        }).ToList();

        var nextCursor = hasMore ? EncodeCursor(invites[limit - 1].Id) : null;

        return new PagedResponse<InviteResponse>(items, nextCursor, hasMore);
    }

    public async Task CancelInviteAsync(Guid tenantId, Guid inviteId, CancellationToken cancellationToken = default)
    {
        var invite = await _context.Invites
            .FirstOrDefaultAsync(i => i.Id == inviteId && i.TenantId == tenantId, cancellationToken)
            ?? throw new NotFoundException("Invite", inviteId);

        invite.Cancel();
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TenantMemberResponse> AcceptInviteAsync(Guid tenantId, string token, Guid userId, CancellationToken cancellationToken = default)
    {
        var invite = await _context.Invites
            .Include(i => i.Tenant)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Token == token, cancellationToken)
            ?? throw new NotFoundException("Invite", "token");

        if (!invite.IsPending)
        {
            if (invite.IsExpired)
            {
                throw new ValidationException("This invitation has expired");
            }
            throw new ValidationException($"This invitation has already been {invite.Status}");
        }

        // Get user
        var user = await _context.Users
            .Include(u => u.Identifiers)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        // Verify user email matches invite email
        var userEmail = user.Identifiers.FirstOrDefault(i => i.Type == "email")?.ValueNormalized;
        if (userEmail != invite.Email)
        {
            throw new ForbiddenException("This invitation was sent to a different email address");
        }

        // Check if user is already a member
        var existingMember = await _context.TenantMembers
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId && m.Status != "removed", cancellationToken);

        if (existingMember != null)
        {
            throw new ConflictException("You are already a member of this tenant");
        }

        // Accept invite
        invite.Accept();

        // Add user as member
        var member = invite.Tenant.AddMember(user);
        await _context.SaveChangesAsync(cancellationToken);

        // Assign roles from invite
        if (invite.RoleIds.Count > 0)
        {
            var roles = await _context.Roles
                .Where(r => invite.RoleIds.Contains(r.Id) && r.TenantId == tenantId)
                .ToListAsync(cancellationToken);

            foreach (var role in roles)
            {
                member.AssignRole(role, invite.InvitedByMemberId);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Reload member with all relationships
        var fullMember = await _context.TenantMembers
            .Include(m => m.User)
                .ThenInclude(u => u.Identifiers)
            .Include(m => m.RoleAssignments)
                .ThenInclude(ra => ra.Role)
            .Include(m => m.Tenant)
            .FirstAsync(m => m.Id == member.Id, cancellationToken);

        return MapMemberToResponse(fullMember);
    }

    private static InviteResponse MapToResponse(Invite invite, string? inviterEmail)
    {
        return new InviteResponse(
            IdPrefix.EncodeInvite(invite.Id),
            invite.Email,
            invite.Status,
            IdPrefix.EncodeMember(invite.InvitedByMemberId),
            inviterEmail,
            invite.RoleIds.Count > 0 ? invite.RoleIds.Select(IdPrefix.EncodeRole).ToList() : null,
            invite.ExpiresAt,
            invite.CreatedAt);
    }

    private static TenantMemberResponse MapMemberToResponse(TenantMember member)
    {
        var email = member.User.Identifiers.FirstOrDefault(i => i.Type == "email")?.ValueNormalized ?? "";
        var username = member.User.Identifiers.FirstOrDefault(i => i.Type == "username")?.ValueNormalized;

        var roles = member.RoleAssignments.Select(ra => ra.Role.Code).ToList();

        return new TenantMemberResponse(
            IdPrefix.EncodeMember(member.Id),
            IdPrefix.EncodeUser(member.UserId),
            email,
            username,
            member.Status,
            member.Tenant.OwnerMemberId == member.Id,
            roles,
            member.JoinedAt);
    }

    private static string EncodeCursor(Guid id) => Convert.ToBase64String(id.ToByteArray());

    private static Guid DecodeCursor(string cursor)
    {
        try
        {
            return new Guid(Convert.FromBase64String(cursor));
        }
        catch
        {
            throw new ValidationException("Invalid cursor format");
        }
    }
}
