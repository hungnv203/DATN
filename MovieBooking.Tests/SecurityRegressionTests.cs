using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using MovieBooking.Application.Common.DTOs.Auth;
using MovieBooking.Controllers;
using MovieBooking.Infrastructure.Security;
using MovieBooking.Infrastructure.Services.Payment;
using Xunit;

namespace MovieBooking.Tests;

public class SecurityRegressionTests
{
    [Fact]
    public void CustomerBookingCreation_RequiresAuthenticationAndSkipsAdminPermission()
    {
        var controllerAuthorization = typeof(CrudController<,>)
            .GetCustomAttribute<AuthorizeAttribute>();
        var action = typeof(BookingsController).GetMethod(nameof(BookingsController.Create));

        Assert.NotNull(controllerAuthorization);
        Assert.NotNull(action);
        Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(action.GetCustomAttribute<SkipPermissionAttribute>());
    }

    [Fact]
    public void RefundAndCheckIn_RequireSpecializedPermissions()
    {
        var refund = typeof(PaymentsController).GetMethod(nameof(PaymentsController.RefundPayment));
        var checkIn = typeof(TicketsController).GetMethod(nameof(TicketsController.CheckIn));

        Assert.Equal("Refund", refund?.GetCustomAttribute<HasPermissionAttribute>()?.Action);
        Assert.Equal("CheckIn", checkIn?.GetCustomAttribute<HasPermissionAttribute>()?.Action);
    }

    [Fact]
    public void AuthenticationRequests_RejectMalformedInput()
    {
        var signUp = new SignUpRequest
        {
            FullName = "A",
            Email = "not-an-email",
            PhoneNumber = "invalid phone !",
            Password = "short"
        };

        var results = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(
            signUp,
            new ValidationContext(signUp),
            results,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void VnPayCallback_WithInvalidSignature_IsRejectedWithoutThrowing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VnPay:HashSecret"] = "test-secret"
            })
            .Build();
        var service = new VnPayService(configuration);
        var payload = new Dictionary<string, string>
        {
            ["vnp_TxnRef"] = Guid.NewGuid().ToString("N"),
            ["vnp_TransactionNo"] = "not-a-number",
            ["vnp_Amount"] = "invalid",
            ["vnp_ResponseCode"] = "00",
            ["vnp_SecureHash"] = "invalid-signature"
        };

        var response = service.PaymentExecute(payload);

        Assert.False(response.Success);
    }
}
