using System;

namespace MovieBooking.Application.Common.DTOs;

public class ShowtimeSeatDto
{
    public Guid SeatId { get; set; }
    public string RowLabel { get; set; } = string.Empty;
    public int SeatNumber { get; set; }
    public string Type { get; set; } = string.Empty; // Standard, VIP, Couple
    public string Status { get; set; } = string.Empty; // Available, Held, Booked
    public Guid? HeldByUserId { get; set; }
}
