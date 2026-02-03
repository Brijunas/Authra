namespace Authra.Application.Common.DTOs;

/// <summary>
/// Cursor-based pagination request parameters.
/// </summary>
public record PaginationRequest(
    string? Cursor = null,
    int Limit = 20);

/// <summary>
/// Cursor-based pagination response wrapper.
/// </summary>
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore);
