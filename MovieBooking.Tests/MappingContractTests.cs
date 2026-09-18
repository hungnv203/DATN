using System;
using System.Collections.Generic;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Mapping;
using Xunit;

namespace MovieBooking.Tests;

public sealed class MappingContractTests
{
    private readonly IMapper _mapper;

    public MappingContractTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddProfile<EntityDtoProfile>());
        var provider = services.BuildServiceProvider();
        _mapper = provider.GetRequiredService<IMapper>();
    }

    [Fact]
    public void BookingToBookingDto_MapsAllEnrichedFields_WithoutDateTimeOffsetException()
    {
        var cinema = new Cinema { Name = "CGV Vincom" };
        var room = new Room { Name = "Cinema 1", Cinema = cinema };
        var movie = new Movie { Title = "Dune: Part Two" };
        var showtime = new Showtime
        {
            Movie = movie,
            Room = room,
            StartTime = DateTime.UtcNow.AddHours(2)
        };
        var user = new User
        {
            FullName = "Nguyen Van A",
            Email = "nguyenvana@gmail.com",
            PhoneNumber = "0912345678"
        };
        var seat = new Seat { RowLabel = "F", SeatNumber = 7 };
        var ticket = new Ticket { Seat = seat, Price = 120000 };
        var concession = new Concession { Name = "Bap Pho Mai", ImageUrl = "https://example.com/popcorn.png" };
        var bookingConcession = new BookingConcession { Concession = concession, Quantity = 2, Price = 50000 };
        var payment = new Payment { Method = "VNPAY", Status = "Success" };

        var booking = new Booking
        {
            Showtime = showtime,
            User = user,
            Payment = payment,
            Tickets = new List<Ticket> { ticket },
            BookingConcessions = new List<BookingConcession> { bookingConcession },
            TotalPrice = 220000,
            Status = "Paid"
        };

        var dto = _mapper.Map<BookingDto>(booking);

        Assert.NotNull(dto);
        Assert.Equal("Dune: Part Two", dto.MovieTitle);
        Assert.Equal("CGV Vincom", dto.CinemaName);
        Assert.Equal("Cinema 1", dto.RoomName);
        Assert.Equal("Nguyen Van A", dto.CustomerName);
        Assert.Equal("nguyenvana@gmail.com", dto.CustomerEmail);
        Assert.Equal("0912345678", dto.CustomerPhone);
        Assert.Equal("VNPAY", dto.PaymentMethod);
        Assert.Single(dto.SeatLabels);
        Assert.Equal("F7", dto.SeatLabels[0]);
        Assert.True(dto.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public void TicketToTicketDto_MapsAllEnrichedFields_WithoutDateTimeOffsetException()
    {
        var cinema = new Cinema { Name = "CGV Landmark" };
        var room = new Room { Name = "IMAX", Cinema = cinema };
        var movie = new Movie { Title = "Avatar 3" };
        var showtime = new Showtime
        {
            Movie = movie,
            Room = room,
            StartTime = DateTime.UtcNow.AddDays(1)
        };
        var user = new User
        {
            FullName = "Tran Thi B",
            Email = "tranthib@gmail.com"
        };
        var booking = new Booking
        {
            Showtime = showtime,
            User = user,
            Status = "Paid"
        };
        var seat = new Seat { RowLabel = "H", SeatNumber = 12 };
        var ticket = new Ticket
        {
            Booking = booking,
            Seat = seat,
            Price = 180000,
            QrCode = "QR_AVATAR_H12",
            Status = "Booked"
        };

        var dto = _mapper.Map<TicketDto>(ticket);

        Assert.NotNull(dto);
        Assert.Equal("Avatar 3", dto.MovieTitle);
        Assert.Equal("CGV Landmark", dto.CinemaName);
        Assert.Equal("IMAX", dto.RoomName);
        Assert.Equal("Tran Thi B", dto.CustomerName);
        Assert.Equal("tranthib@gmail.com", dto.CustomerEmail);
        Assert.Equal("H12", dto.SeatLabel);
        Assert.Equal("Paid", dto.PaymentStatus);
        Assert.Equal("QR_AVATAR_H12", dto.QrCode);
        Assert.True(dto.CreatedAt > DateTime.MinValue);
    }

    [Fact]
    public void MovieToMovieDto_MapsGenresAndGenreIds_Correctly()
    {
        var actionGenre = new Genre { Name = "Action" };
        var sciFiGenre = new Genre { Name = "Sci-Fi" };
        var movie = new Movie
        {
            Title = "Inception",
            MovieGenres = new List<MovieGenre>
            {
                new MovieGenre { Genre = actionGenre, GenreId = actionGenre.Id },
                new MovieGenre { Genre = sciFiGenre, GenreId = sciFiGenre.Id }
            }
        };

        var dto = _mapper.Map<MovieDto>(movie);

        Assert.NotNull(dto);
        Assert.Equal("Inception", dto.Title);
        Assert.Equal(2, dto.Genres.Count);
        Assert.Contains("Action", dto.Genres);
        Assert.Contains("Sci-Fi", dto.Genres);
        Assert.Equal(2, dto.GenreIds.Count);
    }
}
