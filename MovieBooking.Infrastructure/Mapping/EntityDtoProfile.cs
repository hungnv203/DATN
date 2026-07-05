using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Infrastructure.Mapping;

public class EntityDtoProfile : Profile
{
    public EntityDtoProfile()
    {
        CreateMap<User, UserDto>();
        CreateMap<UserDto, User>().IgnoreBaseEntityFromDto();

        CreateMap<Role, RoleDto>();
        CreateMap<RoleDto, Role>().IgnoreBaseEntityFromDto();

        CreateMap<UserRole, UserRoleDto>();
        CreateMap<UserRoleDto, UserRole>().IgnoreBaseEntityFromDto();

        CreateMap<Permission, PermissionDto>();
        CreateMap<PermissionDto, Permission>().IgnoreBaseEntityFromDto();

        CreateMap<RolePermission, RolePermissionDto>();
        CreateMap<RolePermissionDto, RolePermission>().IgnoreBaseEntityFromDto();

        CreateMap<Cinema, CinemaDto>();
        CreateMap<CinemaDto, Cinema>().IgnoreBaseEntityFromDto();

        CreateMap<Room, RoomDto>();
        CreateMap<RoomDto, Room>().IgnoreBaseEntityFromDto();

        CreateMap<Seat, SeatDto>();
        CreateMap<SeatDto, Seat>().IgnoreBaseEntityFromDto();

        CreateMap<Movie, MovieDto>();
        CreateMap<MovieDto, Movie>().IgnoreBaseEntityFromDto();

        CreateMap<Genre, GenreDto>();
        CreateMap<GenreDto, Genre>().IgnoreBaseEntityFromDto();

        CreateMap<MovieGenre, MovieGenreDto>();
        CreateMap<MovieGenreDto, MovieGenre>().IgnoreBaseEntityFromDto();

        CreateMap<Showtime, ShowtimeDto>();
        CreateMap<ShowtimeDto, Showtime>().IgnoreBaseEntityFromDto();

        CreateMap<Booking, BookingDto>();
        CreateMap<BookingDto, Booking>().IgnoreBaseEntityFromDto();

        CreateMap<Ticket, TicketDto>();
        CreateMap<TicketDto, Ticket>().IgnoreBaseEntityFromDto();

        CreateMap<SeatHold, SeatHoldDto>();
        CreateMap<SeatHoldDto, SeatHold>().IgnoreBaseEntityFromDto();

        CreateMap<Payment, PaymentDto>();
        CreateMap<PaymentDto, Payment>().IgnoreBaseEntityFromDto();

        CreateMap<PaymentLog, PaymentLogDto>();
        CreateMap<PaymentLogDto, PaymentLog>().IgnoreBaseEntityFromDto();

        CreateMap<Promotion, PromotionDto>();
        CreateMap<PromotionDto, Promotion>().IgnoreBaseEntityFromDto();

        CreateMap<BookingPromotion, BookingPromotionDto>();
        CreateMap<BookingPromotionDto, BookingPromotion>().IgnoreBaseEntityFromDto();

        CreateMap<LoyaltyPoint, LoyaltyPointDto>();
        CreateMap<LoyaltyPointDto, LoyaltyPoint>().IgnoreBaseEntityFromDto();

        CreateMap<PointTransaction, PointTransactionDto>();
        CreateMap<PointTransactionDto, PointTransaction>().IgnoreBaseEntityFromDto();

        CreateMap<Notification, NotificationDto>();
        CreateMap<NotificationDto, Notification>().IgnoreBaseEntityFromDto();

        CreateMap<Concession, ConcessionDto>();
        CreateMap<ConcessionDto, Concession>().IgnoreBaseEntityFromDto();

        CreateMap<BookingConcession, BookingConcessionDto>();
        CreateMap<BookingConcessionDto, BookingConcession>().IgnoreBaseEntityFromDto();
    }
}
