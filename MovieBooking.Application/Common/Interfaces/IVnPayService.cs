using System.Collections.Generic;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Application.Common.Interfaces;

public interface IVnPayService
{
    string CreatePaymentUrl(string ipAddress, Payment payment, Booking booking);
    VnPayResponseModel PaymentExecute(IEnumerable<KeyValuePair<string, string>> collections);
}

public class VnPayResponseModel
{
    public bool Success { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string OrderDescription { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string PaymentId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string VnPayResponseCode { get; set; } = string.Empty;
}
