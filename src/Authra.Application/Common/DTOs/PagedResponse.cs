namespace Authra.Application.Common.DTOs;

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextCursor,
    bool HasMore);
