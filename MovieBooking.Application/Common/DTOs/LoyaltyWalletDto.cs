namespace MovieBooking.Application.Common.DTOs;

public class LoyaltyWalletDto
{
    public Guid UserId { get; set; }
    public int Points { get; set; }
    public IReadOnlyList<PointTransactionDto> Transactions { get; set; } = Array.Empty<PointTransactionDto>();
}
