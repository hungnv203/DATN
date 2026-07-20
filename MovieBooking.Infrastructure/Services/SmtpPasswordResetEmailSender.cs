using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using MovieBooking.Application.Common.Interfaces;

namespace MovieBooking.Infrastructure.Services;

public class SmtpPasswordResetEmailSender : IPasswordResetEmailSender
{
    private readonly IConfiguration _configuration;

    public SmtpPasswordResetEmailSender(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        var host = _configuration["Smtp:Host"];
        var userName = _configuration["Smtp:UserName"];
        var password = _configuration["Smtp:Password"];
        var fromAddress = _configuration["Smtp:FromAddress"];
        if (string.IsNullOrWhiteSpace(host)
            || string.IsNullOrWhiteSpace(userName)
            || string.IsNullOrWhiteSpace(password)
            || string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("SMTP password-reset delivery is not configured.");
        }

        var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort)
            ? configuredPort
            : 587;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(userName, password)
        };
        using var message = new MailMessage(
            fromAddress,
            email,
            "MovieBooking password reset",
            $"Use this one-time token to reset your password: {resetToken}\n\nThe token expires in one hour.");

        await client.SendMailAsync(message, cancellationToken);
    }
}
