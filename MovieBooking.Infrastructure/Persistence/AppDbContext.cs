using Microsoft.EntityFrameworkCore;
using MovieBooking.Domain.Entities;

namespace MovieBooking.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<BookingPromotion> BookingPromotions => Set<BookingPromotion>();
    public DbSet<LoyaltyPoint> LoyaltyPoints => Set<LoyaltyPoint>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
            .HasIndex(x => new { x.UserId, x.RoleId })
            .IsUnique();

        modelBuilder.Entity<MovieGenre>()
            .HasIndex(x => new { x.MovieId, x.GenreId })
            .IsUnique();

        modelBuilder.Entity<BookingPromotion>()
            .HasIndex(x => new { x.BookingId, x.PromotionId })
            .IsUnique();

        modelBuilder.Entity<LoyaltyPoint>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        modelBuilder.Entity<Payment>()
            .HasOne(x => x.Booking)
            .WithOne(x => x.Payment)
            .HasForeignKey<Payment>(x => x.BookingId);

        modelBuilder.Entity<Booking>()
            .HasOne(x => x.User)
            .WithMany(x => x.Bookings)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SeatHold>()
            .HasOne(x => x.User)
            .WithMany(x => x.SeatHolds)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LoyaltyPoint>()
            .HasOne(x => x.User)
            .WithOne(x => x.LoyaltyPoint)
            .HasForeignKey<LoyaltyPoint>(x => x.UserId);
    }
}
