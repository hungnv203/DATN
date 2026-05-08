namespace MovieBooking.Application.Common.DTOs;

public class LoyaltyPointDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int Points { get; set; }
}
