using System;
using System.Collections.Generic;

namespace MovieBooking.Application.Common.DTOs;

public class MyTicketDto
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string MovieTitle { get; set; } = string.Empty;
    public string CinemaName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string SeatLabel { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public List<TicketConcessionDto> Concessions { get; set; } = new();
}

public class TicketConcessionDto
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
}
