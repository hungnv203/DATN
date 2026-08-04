using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Controllers;
using MovieBooking.Domain.Constants;
using MovieBooking.Domain.Entities;
using Xunit;

namespace MovieBooking.Tests;

public sealed class PaymentWorkflowContractTests
{
    [Fact]
    public void CanonicalStates_KeepPendingSeparateFromPaidAndBooked()
    {
        Assert.Equal("PointOfSale", BookingChannels.PointOfSale);
        Assert.Equal("Pending", BookingStatuses.Pending);
        Assert.Equal("Paid", BookingStatuses.Paid);
        Assert.Equal("Success", PaymentStatuses.Success);
        Assert.Equal("Held", TicketStatuses.Held);
        Assert.Equal("Booked", TicketStatuses.Booked);
        Assert.Equal("Completed", SeatHoldStatuses.Completed);
    }

    [Fact]
    public void PaymentOperation_UsesSeparateIdempotencyDomains()
    {
        var operation = new PaymentOperation
        {
            ClientIdempotencyKey = Guid.NewGuid(),
            ProviderEventKey = null
        };

        Assert.NotNull(operation.ClientIdempotencyKey);
        Assert.Null(operation.ProviderEventKey);
        Assert.Equal("RedeemReturn", LoyaltyEffectTypes.RedeemReturn);
    }

    [Fact]
    public void PosCommands_ExposeBoundedRoutes()
    {
        var confirmation = typeof(BookingsController)
            .GetMethod(nameof(BookingsController.ConfirmPointOfSalePayment));
        var cancellation = typeof(BookingsController)
            .GetMethod(nameof(BookingsController.CancelPointOfSale));

        Assert.Equal(
            "{bookingId:guid}/pos-payment-confirmations",
            confirmation?.GetCustomAttribute<HttpPostAttribute>()?.Template);
        Assert.Equal(
            "{bookingId:guid}/pos-cancellations",
            cancellation?.GetCustomAttribute<HttpPostAttribute>()?.Template);
    }

    [Fact]
    public void VnPay_SeparatesMutationIpnFromInformationalReturn()
    {
        var ipn = typeof(PaymentsController).GetMethod(nameof(PaymentsController.VnPayIpn));
        var browserReturn = typeof(PaymentsController)
            .GetMethod(nameof(PaymentsController.VnPayReturn));

        Assert.Equal("vnpay-ipn", ipn?.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal(
            "vnpay-return",
            browserReturn?.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public void VnPay_OnlyDocumentsKnownConfirmedFailureStatuses()
    {
        Assert.Contains("24", VnPayStatuses.ConfirmedFailureResponseCodes);
        Assert.Contains("02", VnPayStatuses.ConfirmedFailureTransactionStatuses);
        Assert.DoesNotContain("XX", VnPayStatuses.ConfirmedFailureResponseCodes);
        Assert.DoesNotContain("YY", VnPayStatuses.ConfirmedFailureTransactionStatuses);
    }

    [Fact]
    public void PosCreateRequest_DoesNotAcceptIdentityOrMoney()
    {
        var properties = typeof(CreatePosBookingRequestDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(
            ["ShowtimeId", "SeatIds", "SeatHoldGroupId"],
            properties);
    }
}
