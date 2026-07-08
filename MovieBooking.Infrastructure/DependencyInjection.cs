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

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString)
                   .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddAutoMapper(cfg => cfg.AddProfile<EntityDtoProfile>());

        services.AddScoped(typeof(ICrudService<,>), typeof(EfCrudService<,>));
        services.AddScoped<ICrudService<User, UserDto>, UserCrudService>();
        services.AddScoped<IBookingService, BookingCrudService>();
        services.AddScoped<ICrudService<Booking, BookingDto>>(provider => provider.GetRequiredService<IBookingService>());
        services.AddScoped<ICrudService<Ticket, TicketDto>, TicketCrudService>();
        services.AddScoped<IShowtimeService, ShowtimeCrudService>();
        services.AddScoped<ICrudService<Showtime, ShowtimeDto>>(provider => provider.GetRequiredService<IShowtimeService>());
        services.AddScoped<IImageUploadService, CloudinaryImageUploadService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IVnPayService, VnPayService>();
        services.AddHostedService<ExpiredSeatHoldsCleanupService>();
        services.AddHostedService<ExpiredBookingsCleanupService>();

        services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}
