using Microsoft.AspNetCore.Mvc;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Common;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Controllers;

[ApiController]
public abstract class CrudController<TEntity, TDto> : ControllerBase
    where TEntity : BaseEntity, new()
    where TDto : class, new()
{
    private readonly ICrudService<TEntity, TDto> _crudService;

    protected CrudController(ICrudService<TEntity, TDto> crudService)
    {
        _crudService = crudService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _crudService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _crudService.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<TDto>> Create([FromBody] TDto dto, CancellationToken cancellationToken)
    {
        var created = await _crudService.CreateAsync(dto, cancellationToken);
        var id = (Guid?)typeof(TDto).GetProperty("Id")?.GetValue(created) ?? Guid.Empty;
        return CreatedAtAction(nameof(GetById), new { id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] TDto dto, CancellationToken cancellationToken)
    {
        var updated = await _crudService.UpdateAsync(id, dto, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _crudService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

[Route("api/users")]
public class UsersController : CrudController<User, UserDto>
{
    public UsersController(ICrudService<User, UserDto> crudService) : base(crudService) { }
}

[Route("api/roles")]
public class RolesController : CrudController<Role, RoleDto>
{
    public RolesController(ICrudService<Role, RoleDto> crudService) : base(crudService) { }
}

[Route("api/user-roles")]
public class UserRolesController : CrudController<UserRole, UserRoleDto>
{
    public UserRolesController(ICrudService<UserRole, UserRoleDto> crudService) : base(crudService) { }
}

[Route("api/cinemas")]
public class CinemasController : CrudController<Cinema, CinemaDto>
{
    public CinemasController(ICrudService<Cinema, CinemaDto> crudService) : base(crudService) { }
}

[Route("api/rooms")]
public class RoomsController : CrudController<Room, RoomDto>
{
    public RoomsController(ICrudService<Room, RoomDto> crudService) : base(crudService) { }
}

[Route("api/seats")]
public class SeatsController : CrudController<Seat, SeatDto>
{
    public SeatsController(ICrudService<Seat, SeatDto> crudService) : base(crudService) { }
}

[Route("api/movies")]
public class MoviesController : CrudController<Movie, MovieDto>
{
    public MoviesController(ICrudService<Movie, MovieDto> crudService) : base(crudService) { }
}

[Route("api/genres")]
public class GenresController : CrudController<Genre, GenreDto>
{
    public GenresController(ICrudService<Genre, GenreDto> crudService) : base(crudService) { }
}

[Route("api/movie-genres")]
public class MovieGenresController : CrudController<MovieGenre, MovieGenreDto>
{
    public MovieGenresController(ICrudService<MovieGenre, MovieGenreDto> crudService) : base(crudService) { }
}

[Route("api/showtimes")]
public class ShowtimesController : CrudController<Showtime, ShowtimeDto>
{
    public ShowtimesController(ICrudService<Showtime, ShowtimeDto> crudService) : base(crudService) { }
}

[Route("api/bookings")]
public class BookingsController : CrudController<Booking, BookingDto>
{
    public BookingsController(ICrudService<Booking, BookingDto> crudService) : base(crudService) { }
}

[Route("api/tickets")]
public class TicketsController : CrudController<Ticket, TicketDto>
{
    public TicketsController(ICrudService<Ticket, TicketDto> crudService) : base(crudService) { }
}

[Route("api/seat-holds")]
public class SeatHoldsController : CrudController<SeatHold, SeatHoldDto>
{
    public SeatHoldsController(ICrudService<SeatHold, SeatHoldDto> crudService) : base(crudService) { }
}

[Route("api/payments")]
public class PaymentsController : CrudController<Payment, PaymentDto>
{
    public PaymentsController(ICrudService<Payment, PaymentDto> crudService) : base(crudService) { }
}

[Route("api/payment-logs")]
public class PaymentLogsController : CrudController<PaymentLog, PaymentLogDto>
{
    public PaymentLogsController(ICrudService<PaymentLog, PaymentLogDto> crudService) : base(crudService) { }
}

[Route("api/promotions")]
public class PromotionsController : CrudController<Promotion, PromotionDto>
{
    public PromotionsController(ICrudService<Promotion, PromotionDto> crudService) : base(crudService) { }
}

[Route("api/booking-promotions")]
public class BookingPromotionsController : CrudController<BookingPromotion, BookingPromotionDto>
{
    public BookingPromotionsController(ICrudService<BookingPromotion, BookingPromotionDto> crudService) : base(crudService) { }
}

[Route("api/loyalty-points")]
public class LoyaltyPointsController : CrudController<LoyaltyPoint, LoyaltyPointDto>
{
    public LoyaltyPointsController(ICrudService<LoyaltyPoint, LoyaltyPointDto> crudService) : base(crudService) { }
}

[Route("api/point-transactions")]
public class PointTransactionsController : CrudController<PointTransaction, PointTransactionDto>
{
    public PointTransactionsController(ICrudService<PointTransaction, PointTransactionDto> crudService) : base(crudService) { }
}

[Route("api/notifications")]
public class NotificationsController : CrudController<Notification, NotificationDto>
{
    public NotificationsController(ICrudService<Notification, NotificationDto> crudService) : base(crudService) { }
}
