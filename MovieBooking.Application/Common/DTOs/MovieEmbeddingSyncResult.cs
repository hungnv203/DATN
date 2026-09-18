namespace MovieBooking.Application.Common.DTOs;

public sealed record MovieEmbeddingSyncResult(
    string Status,
    string ContentHash = "",
    string? Error = null);
