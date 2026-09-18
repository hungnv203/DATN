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

        CreateMap<Movie, MovieDto>()
            .ForMember(dest => dest.Genres, opt => opt.MapFrom(src => src.MovieGenres != null ? src.MovieGenres.Where(mg => mg.Genre != null).Select(mg => mg.Genre.Name).OrderBy(n => n).ToList() : new List<string>()))
            .ForMember(dest => dest.GenreIds, opt => opt.MapFrom(src => src.MovieGenres != null ? src.MovieGenres.Select(mg => mg.GenreId).ToList() : new List<Guid>()));
        CreateMap<MovieDto, Movie>()
            .IgnoreBaseEntityFromDto()
            .ForMember(dest => dest.MovieGenres, opt => opt.Ignore());

        CreateMap<Genre, GenreDto>();
        CreateMap<GenreDto, Genre>().IgnoreBaseEntityFromDto();

        CreateMap<MovieGenre, MovieGenreDto>();
        CreateMap<MovieGenreDto, MovieGenre>().IgnoreBaseEntityFromDto();

        CreateMap<Showtime, ShowtimeDto>();
        CreateMap<ShowtimeDto, Showtime>().IgnoreBaseEntityFromDto();

        CreateMap<DateTimeOffset, DateTime>().ConvertUsing(src => src.UtcDateTime);
        CreateMap<DateTimeOffset?, DateTime?>().ConvertUsing(src => src.HasValue ? src.Value.UtcDateTime : null);

        CreateMap<Booking, BookingDto>()
            .ForMember(dest => dest.SeatIds, opt => opt.MapFrom(src => src.Tickets.Select(t => t.SeatId).ToList()))
            .ForMember(dest => dest.Concessions, opt => opt.MapFrom(src => src.BookingConcessions))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt.UtcDateTime))
            .ForMember(dest => dest.MovieTitle, opt => opt.MapFrom(src => src.Showtime != null && src.Showtime.Movie != null ? src.Showtime.Movie.Title : string.Empty))
            .ForMember(dest => dest.CinemaName, opt => opt.MapFrom(src => src.Showtime != null && src.Showtime.Room != null && src.Showtime.Room.Cinema != null ? src.Showtime.Room.Cinema.Name : string.Empty))
            .ForMember(dest => dest.RoomName, opt => opt.MapFrom(src => src.Showtime != null && src.Showtime.Room != null ? src.Showtime.Room.Name : string.Empty))
            .ForMember(dest => dest.ShowtimeStartTime, opt => opt.MapFrom(src => src.Showtime != null ? src.Showtime.StartTime : default))
            .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.User != null ? src.User.FullName : string.Empty))
            .ForMember(dest => dest.CustomerEmail, opt => opt.MapFrom(src => src.User != null ? src.User.Email : string.Empty))
            .ForMember(dest => dest.CustomerPhone, opt => opt.MapFrom(src => src.User != null ? src.User.PhoneNumber : string.Empty))
            .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.Payment != null ? src.Payment.Method : string.Empty))
            .ForMember(dest => dest.SeatLabels, opt => opt.MapFrom(src => src.Tickets.Select(t => t.Seat != null ? t.Seat.RowLabel + t.Seat.SeatNumber : string.Empty).ToList()));
        CreateMap<BookingDto, Booking>().IgnoreBaseEntityFromDto();

        CreateMap<Ticket, TicketDto>()
            .ForMember(dest => dest.MovieTitle, opt => opt.MapFrom(src => src.Booking != null && src.Booking.Showtime != null && src.Booking.Showtime.Movie != null ? src.Booking.Showtime.Movie.Title : string.Empty))
            .ForMember(dest => dest.SeatLabel, opt => opt.MapFrom(src => src.Seat != null ? src.Seat.RowLabel + src.Seat.SeatNumber : string.Empty))
            .ForMember(dest => dest.PaymentStatus, opt => opt.MapFrom(src => src.Booking != null ? src.Booking.Status : string.Empty))
            .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt.UtcDateTime))
            .ForMember(dest => dest.CinemaName, opt => opt.MapFrom(src => src.Booking != null && src.Booking.Showtime != null && src.Booking.Showtime.Room != null && src.Booking.Showtime.Room.Cinema != null ? src.Booking.Showtime.Room.Cinema.Name : string.Empty))
            .ForMember(dest => dest.RoomName, opt => opt.MapFrom(src => src.Booking != null && src.Booking.Showtime != null && src.Booking.Showtime.Room != null ? src.Booking.Showtime.Room.Name : string.Empty))
            .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.Booking != null && src.Booking.Showtime != null ? src.Booking.Showtime.StartTime : default))
            .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Booking != null && src.Booking.User != null ? src.Booking.User.FullName : string.Empty))
            .ForMember(dest => dest.CustomerEmail, opt => opt.MapFrom(src => src.Booking != null && src.Booking.User != null ? src.Booking.User.Email : string.Empty));
        CreateMap<TicketDto, Ticket>().IgnoreBaseEntityFromDto();

        CreateMap<SeatHold, SeatHoldDto>();
        CreateMap<SeatHoldDto, SeatHold>().IgnoreBaseEntityFromDto();

        CreateMap<Payment, PaymentDto>();
        CreateMap<PaymentDto, Payment>().IgnoreBaseEntityFromDto();

        CreateMap<PaymentLog, PaymentLogDto>();
        CreateMap<PaymentLogDto, PaymentLog>().IgnoreBaseEntityFromDto();

        CreateMap<Notification, NotificationDto>();
        CreateMap<NotificationDto, Notification>().IgnoreBaseEntityFromDto();

        CreateMap<Concession, ConcessionDto>();
        CreateMap<ConcessionDto, Concession>().IgnoreBaseEntityFromDto();

        CreateMap<BookingConcession, BookingConcessionDto>()
            .ForMember(dest => dest.ConcessionName, opt => opt.MapFrom(src => src.Concession.Name))
            .ForMember(dest => dest.ConcessionImageUrl, opt => opt.MapFrom(src => src.Concession.ImageUrl));
        CreateMap<BookingConcessionDto, BookingConcession>().IgnoreBaseEntityFromDto();
    }
}
