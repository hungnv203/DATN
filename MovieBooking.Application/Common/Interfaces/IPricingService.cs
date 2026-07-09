using MovieBooking.Application.Common.DTOs;

namespace MovieBooking.Application.Common.Interfaces;

public interface IPricingService
{
    Task<BookingQuoteDto> QuoteAsync(
        BookingQuoteRequestDto request,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
