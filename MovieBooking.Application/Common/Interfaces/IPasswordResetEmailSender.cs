namespace MovieBooking.Application.Common.Interfaces;

public interface IPasswordResetEmailSender
{
    Task SendAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default);
}
