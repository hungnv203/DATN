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
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<MovieReview> MovieReviews => Set<MovieReview>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<ShowtimeSeatVersion> ShowtimeSeatVersions => Set<ShowtimeSeatVersion>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SeatHold> SeatHolds => Set<SeatHold>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();
    public DbSet<PaymentOperation> PaymentOperations => Set<PaymentOperation>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<BookingPromotion> BookingPromotions => Set<BookingPromotion>();
    public DbSet<LoyaltyPoint> LoyaltyPoints => Set<LoyaltyPoint>();
    public DbSet<PointTransaction> PointTransactions => Set<PointTransaction>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Concession> Concessions => Set<Concession>();
    public DbSet<BookingConcession> BookingConcessions => Set<BookingConcession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserRole>()
            .HasIndex(x => new { x.UserId, x.RoleId })
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasIndex(x => new { x.RoleId, x.PermissionId })
            .IsUnique();

        modelBuilder.Entity<MovieGenre>()
            .HasIndex(x => new { x.MovieId, x.GenreId })
            .IsUnique();

        modelBuilder.Entity<MovieReview>()
            .HasIndex(x => new { x.MovieId, x.UserId })
            .IsUnique();

        modelBuilder.Entity<BookingPromotion>()
            .HasIndex(x => new { x.BookingId, x.PromotionId })
            .IsUnique();

        modelBuilder.Entity<LoyaltyPoint>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<Payment>()
            .HasOne(x => x.Booking)
            .WithOne(x => x.Payment)
            .HasForeignKey<Payment>(x => x.BookingId);

        modelBuilder.Entity<Payment>()
            .HasIndex(x => x.BookingId)
            .IsUnique();

        modelBuilder.Entity<Payment>()
            .HasIndex(x => new { x.Method, x.TransactionCode })
            .IsUnique()
            .HasFilter("\"TransactionCode\" <> ''");

        modelBuilder.Entity<Booking>()
            .Property(x => x.Channel)
            .HasMaxLength(32)
            .HasDefaultValue(MovieBooking.Domain.Constants.BookingChannels.CustomerOnline);

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

        modelBuilder.Entity<SeatHold>()
            .Property(x => x.Status)
            .HasMaxLength(32);

        modelBuilder.Entity<SeatHold>()
            .HasIndex(x => new { x.ShowtimeId, x.SeatId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");

        modelBuilder.Entity<SeatHold>()
            .HasIndex(x => new { x.HoldGroupId, x.UserId });

        modelBuilder.Entity<SeatHold>()
            .HasIndex(x => new { x.Status, x.ExpiredAt });

        modelBuilder.Entity<Booking>()
            .HasIndex(x => x.SeatHoldGroupId)
            .IsUnique()
            .HasFilter("\"SeatHoldGroupId\" IS NOT NULL");

        modelBuilder.Entity<ShowtimeSeatVersion>()
            .HasKey(x => x.ShowtimeId);
        modelBuilder.Entity<ShowtimeSeatVersion>()
            .Property(x => x.Version)
            .HasDefaultValue(0L);
        modelBuilder.Entity<ShowtimeSeatVersion>()
            .HasOne(x => x.Showtime)
            .WithOne(x => x.SeatVersion)
            .HasForeignKey<ShowtimeSeatVersion>(x => x.ShowtimeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LoyaltyPoint>()
            .HasOne(x => x.User)
            .WithOne(x => x.LoyaltyPoint)
            .HasForeignKey<LoyaltyPoint>(x => x.UserId);

        modelBuilder.Entity<PointTransaction>()
            .HasOne(x => x.Booking)
            .WithMany(x => x.PointTransactions)
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<PointTransaction>()
            .Property(x => x.EffectType)
            .HasMaxLength(32);

        modelBuilder.Entity<PointTransaction>()
            .HasIndex(x => new { x.BookingId, x.EffectType })
            .IsUnique()
            .HasFilter("\"BookingId\" IS NOT NULL AND \"EffectType\" IS NOT NULL");

        modelBuilder.Entity<PaymentOperation>()
            .Property(x => x.ProviderEventKey)
            .HasMaxLength(64)
            .IsFixedLength();

        modelBuilder.Entity<PaymentOperation>()
            .Property(x => x.OperationType)
            .HasMaxLength(32);

        modelBuilder.Entity<PaymentOperation>()
            .Property(x => x.Method)
            .HasMaxLength(16);

        modelBuilder.Entity<PaymentOperation>()
            .Property(x => x.Result)
            .HasMaxLength(32);

        modelBuilder.Entity<PaymentOperation>()
            .Property(x => x.ReasonCode)
            .HasMaxLength(64);

        modelBuilder.Entity<PaymentOperation>()
            .HasIndex(x => x.ClientIdempotencyKey)
            .IsUnique()
            .HasFilter("\"ClientIdempotencyKey\" IS NOT NULL");

        modelBuilder.Entity<PaymentOperation>()
            .HasIndex(x => x.ProviderEventKey)
            .IsUnique()
            .HasFilter("\"ProviderEventKey\" IS NOT NULL");

        modelBuilder.Entity<PaymentOperation>()
            .ToTable(table => table.HasCheckConstraint(
                "CK_PaymentOperations_IdempotencyDomain",
                "(\"ClientIdempotencyKey\" IS NOT NULL AND \"ProviderEventKey\" IS NULL) OR " +
                "(\"ClientIdempotencyKey\" IS NULL AND \"ProviderEventKey\" IS NOT NULL)"));

        modelBuilder.Entity<PaymentOperation>()
            .HasOne(x => x.Booking)
            .WithMany(x => x.PaymentOperations)
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PaymentOperation>()
            .HasOne(x => x.Payment)
            .WithMany(x => x.Operations)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<BookingConcession>()
            .HasOne(x => x.Booking)
            .WithMany(x => x.BookingConcessions)
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BookingConcession>()
            .HasOne(x => x.Concession)
            .WithMany(x => x.BookingConcessions)
            .HasForeignKey(x => x.ConcessionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MovieReview>()
            .HasOne(x => x.Movie)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.MovieId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MovieReview>()
            .HasOne(x => x.User)
            .WithMany(x => x.MovieReviews)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MovieReview>()
            .HasOne(x => x.Booking)
            .WithMany(x => x.MovieReviews)
            .HasForeignKey(x => x.BookingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
