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
        var tick = DateTime.Now.Ticks.ToString();

        var vnpay = new VnPayLibrary();

        vnpay.AddRequestData("vnp_Version", "2.1.0");
        vnpay.AddRequestData("vnp_Command", "pay");
        vnpay.AddRequestData("vnp_TmnCode", _configuration["VnPay:TmnCode"]);
        // Amount must be multiplied by 100
        vnpay.AddRequestData("vnp_Amount", (payment.Amount * 100).ToString("0")); 
        
        // VNPAY requires GMT+7 time
        vnpay.AddRequestData("vnp_CreateDate", payment.CreatedAt.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss"));
        vnpay.AddRequestData("vnp_CurrCode", "VND");
        vnpay.AddRequestData("vnp_IpAddr", string.IsNullOrEmpty(ipAddress) ? "127.0.0.1" : ipAddress);
        vnpay.AddRequestData("vnp_Locale", "vn");

        vnpay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang:" + booking.Id);
        vnpay.AddRequestData("vnp_OrderType", "other"); //default value: other
        vnpay.AddRequestData("vnp_ReturnUrl", _configuration["VnPay:PaymentBackReturnUrl"]);
        
        // Use Payment.Id as TxnRef to map when callback returns
        vnpay.AddRequestData("vnp_TxnRef", payment.Id.ToString());

        var paymentUrl = vnpay.CreateRequestUrl(_configuration["VnPay:BaseUrl"], _configuration["VnPay:HashSecret"]);

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

        var vnp_orderId = Convert.ToInt64(vnpay.GetResponseData("vnp_TxnRef"));
        var vnp_TransactionId = Convert.ToInt64(vnpay.GetResponseData("vnp_TransactionNo"));
        var vnp_SecureHash = collections.FirstOrDefault(p => p.Key == "vnp_SecureHash").Value;
        var vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
        var vnp_OrderInfo = vnpay.GetResponseData("vnp_OrderInfo");

        bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, _configuration["VnPay:HashSecret"]);
        if (!checkSignature)
        {
            return new VnPayResponseModel
            {
                Success = false
            };
        }

        return new VnPayResponseModel
        {
            Success = true,
            PaymentMethod = "VnPay",
            OrderDescription = vnp_OrderInfo,
            OrderId = vnp_orderId.ToString(),
            TransactionId = vnp_TransactionId.ToString(),
            Token = vnp_SecureHash,
            VnPayResponseCode = vnp_ResponseCode
        };
    }
}
