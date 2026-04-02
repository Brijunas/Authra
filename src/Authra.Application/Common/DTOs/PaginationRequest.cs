namespace Authra.Application.Common.DTOs;

public record PaginationRequest(
    string? Cursor = null,
    int Limit = 20);
