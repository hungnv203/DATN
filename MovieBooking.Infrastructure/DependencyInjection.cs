using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MovieBooking.Application.Common.Configuration;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Mapping;
using MovieBooking.Infrastructure.Persistence;
using MovieBooking.Infrastructure.Security;
using MovieBooking.Infrastructure.Services;
using MovieBooking.Infrastructure.Services.Payment;

namespace MovieBooking.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddAutoMapper(cfg => cfg.AddProfile<EntityDtoProfile>());

        services.AddScoped<IBookingPromotionService, BookingPromotionService>();
        services.AddScoped<ICinemaService, CinemaService>();
        services.AddScoped<IConcessionService, ConcessionService>();
        services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<ILoyaltyPointService, LoyaltyPointService>();
        services.AddScoped<IMovieService, MovieService>();
        services.AddScoped<IMovieGenreService, MovieGenreService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IPaymentLogService, PaymentLogService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IPointTransactionService, PointTransactionService>();
        services.AddScoped<IPromotionService, PromotionService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IRolePermissionService, RolePermissionService>();
        services.AddScoped<IRoomService, RoomService>();
        services.AddScoped<ISeatService, SeatService>();
        services.AddScoped<ISeatHoldService, SeatHoldService>();
        services.AddScoped<IUserRoleService, UserRoleService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IBookingService, BookingService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<ILoyaltyService, LoyaltyService>();
        services.AddScoped<IMovieReviewService, MovieReviewService>();
        services.AddScoped<IMovieDiscoveryService, MovieDiscoveryService>();
        services.AddScoped<IPasswordResetEmailSender, SmtpPasswordResetEmailSender>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IShowtimeService, ShowtimeService>();
        services.AddScoped<ISeatLayoutService, SeatLayoutService>();
        services.AddScoped<IImageUploadService, CloudinaryImageUploadService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddScoped<IPaymentWorkflowService, PaymentWorkflowService>();
        services.AddHostedService<ExpiredSeatHoldsCleanupService>();
        services.AddHostedService<ExpiredBookingsCleanupService>();
        services.AddHostedService<MovieStatusUpdateService>();

        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
