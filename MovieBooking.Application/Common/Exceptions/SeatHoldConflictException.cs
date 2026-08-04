namespace MovieBooking.Application.Common.Exceptions;

public sealed class SeatHoldConflictException : Exception
{
    public SeatHoldConflictException(string message) : base(message)
    {
    }
}
