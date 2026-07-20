using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Infrastructure.Services.Payment;

public class VnPayService : IVnPayService
{
    private readonly IConfiguration _configuration;

    public VnPayService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string CreatePaymentUrl(string ipAddress, Domain.Entities.Payment payment, Booking booking)
    {
        var vnpay = new VnPayLibrary();
        var tmnCode = GetRequiredSetting("VnPay:TmnCode");
        var returnUrl = GetRequiredSetting("VnPay:PaymentBackReturnUrl");
        var baseUrl = GetRequiredSetting("VnPay:BaseUrl");
        var hashSecret = GetRequiredSetting("VnPay:HashSecret");

        vnpay.AddRequestData("vnp_Version", "2.1.0");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_TmnCode", tmnCode);
        // Amount must be multiplied by 100
        vnpay.AddRequestData("vnp_Amount", (payment.Amount * 100).ToString("0")); 
        
        // VNPAY requires GMT+7 time
        vnpay.AddRequestData("vnp_CreateDate", payment.CreatedAt.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", "VND");
        vnpay.AddRequestData("vnp_IpAddr", string.IsNullOrEmpty(ipAddress) ? "127.0.0.1" : ipAddress);
        vnpay.AddRequestData("vnp_Locale", "vn");

        vnpay.AddRequestData("vnp_OrderInfo", "ThanhToanDonHang" + booking.Id.ToString("N"));
        vnpay.AddRequestData("vnp_OrderType", "other"); //default value: other
        vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
        
        // Use Payment.Id as TxnRef to map when callback returns, removing hyphens to be safe
        vnpay.AddRequestData("vnp_TxnRef", payment.Id.ToString("N"));

        var paymentUrl = vnpay.CreateRequestUrl(baseUrl, hashSecret);

        return paymentUrl;
    }

    public VnPayResponseModel PaymentExecute(IEnumerable<KeyValuePair<string, string>> collections)
    {
        var vnpay = new VnPayLibrary();
        foreach (var (key, value) in collections)
        {
            if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
            {
                vnpay.AddResponseData(key, value.ToString());
            }
        }

        var vnp_orderId = vnpay.GetResponseData("vnp_TxnRef");
        var transactionNumber = vnpay.GetResponseData("vnp_TransactionNo");
        var amountValue = vnpay.GetResponseData("vnp_Amount");
        var vnp_SecureHash = collections.FirstOrDefault(p => p.Key == "vnp_SecureHash").Value;
        var vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
        var vnp_OrderInfo = vnpay.GetResponseData("vnp_OrderInfo");

        var hashSecret = GetRequiredSetting("VnPay:HashSecret");
        bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, hashSecret);
        if (!checkSignature)
        {
            return new VnPayResponseModel
            {
                Success = false
            };
        }

        if (!long.TryParse(transactionNumber, out var transactionId)
            || !decimal.TryParse(amountValue, out var amount))
        {
            return new VnPayResponseModel { Success = false };
        }

        return new VnPayResponseModel
        {
            Success = true,
            PaymentMethod = "VnPay",
            OrderDescription = vnp_OrderInfo,
            OrderId = vnp_orderId,
            TransactionId = transactionId.ToString(),
            Token = vnp_SecureHash,
            VnPayResponseCode = vnp_ResponseCode,
            Amount = amount / 100m
        };
    }

    private string GetRequiredSetting(string key)
    {
        return _configuration[key]
            ?? throw new InvalidOperationException($"Configuration value '{key}' is missing.");
    }
}
