namespace MovieBooking.Domain.Constants;

public static class SeatHoldErrors
{
    public const string HoldSeatLimitExceeded = "HOLD_SEAT_LIMIT_EXCEEDED";
    public const string ShowtimeNotBookable = "SHOWTIME_NOT_BOOKABLE";
    public const string HoldAlreadyBooked = "HOLD_ALREADY_BOOKED";
    public const string BookingAlreadyPending = "BOOKING_ALREADY_PENDING";
    public const string SeatNotAvailable = "SEAT_NOT_AVAILABLE";
    public const string ShowtimeNotFound = "SHOWTIME_NOT_FOUND";
    public const string HoldNotFound = "HOLD_NOT_FOUND";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InvalidSeatSelection = "INVALID_SEAT_SELECTION";

    public const int MaxSeatsPerHold = 8;
    public const int HoldTtlMinutes = 5;
    public const int RateLimitPerMinute = 10;
}
