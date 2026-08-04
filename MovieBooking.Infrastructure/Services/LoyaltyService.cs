using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Domain.Constants;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class LoyaltyService : ILoyaltyService
{
    private readonly AppDbContext _db;
    private readonly IMapper _mapper;

    public LoyaltyService(AppDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<LoyaltyWalletDto> GetWalletAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var wallet = await _db.LoyaltyPoints
            .AsNoTracking()
            .FirstOrDefaultAsync(lp => lp.UserId == userId, cancellationToken);
        var transactions = await _db.PointTransactions
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        return new LoyaltyWalletDto
        {
            UserId = userId,
            Points = wallet?.Points ?? 0,
            Transactions = transactions.Select(t => _mapper.Map<PointTransactionDto>(t)).ToList()
        };
    }

    public async Task EarnForBookingAsync(Guid bookingId, decimal paidAmount, CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking == null)
        {
            return;
        }

        var alreadyEarned = await _db.PointTransactions.AnyAsync(
            t => t.BookingId == bookingId && t.EffectType == LoyaltyEffectTypes.Earn,
            cancellationToken);
        if (alreadyEarned)
        {
            return;
        }

        var earnedPoints = (int)(paidAmount * 0.01m);
        if (earnedPoints <= 0)
        {
            return;
        }

        await AddTransactionAsync(
            booking.UserId,
            bookingId,
            earnedPoints,
            "Earn",
            LoyaltyEffectTypes.Earn,
            "Earned points from paid booking.",
            cancellationToken);
    }

    public async Task RefundForBookingAsync(Guid bookingId, decimal paidAmount, CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking == null)
        {
            return;
        }

        var alreadyRefunded = await _db.PointTransactions.AnyAsync(
            t => t.BookingId == bookingId && t.Type == "Refund",
            cancellationToken);
        if (alreadyRefunded)
        {
            return;
        }

        var refundedPoints = (int)(paidAmount * 0.01m);
        if (refundedPoints > 0)
        {
            await AddTransactionAsync(
                booking.UserId,
                bookingId,
            -refundedPoints,
            "Refund",
            null,
                "Reverted earned points after refund.",
                cancellationToken);
        }

        await ReturnRedeemedPointsAsync(bookingId, cancellationToken);
    }

    public async Task RedeemForBookingAsync(Guid bookingId, int usedPoints, CancellationToken cancellationToken = default)
    {
        if (usedPoints <= 0)
        {
            return;
        }

        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking == null)
        {
            return;
        }

        var alreadyRedeemed = await _db.PointTransactions.AnyAsync(
            t => t.BookingId == bookingId && t.EffectType == LoyaltyEffectTypes.Redeem,
            cancellationToken);
        if (alreadyRedeemed)
        {
            return;
        }

        await AddTransactionAsync(
            booking.UserId,
            bookingId,
            -usedPoints,
            "Redeem",
            LoyaltyEffectTypes.Redeem,
            "Redeemed points for booking discount.",
            cancellationToken);
    }

    public async Task ReturnRedeemedPointsAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking == null)
        {
            return;
        }

        var redeemedTransaction = await _db.PointTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.BookingId == bookingId && t.EffectType == LoyaltyEffectTypes.Redeem,
                cancellationToken);
        var alreadyReturnedRedeem = await _db.PointTransactions.AnyAsync(
            t => t.BookingId == bookingId && t.EffectType == LoyaltyEffectTypes.RedeemReturn,
            cancellationToken);
        if (redeemedTransaction == null || alreadyReturnedRedeem)
        {
            return;
        }

        await AddTransactionAsync(
            booking.UserId,
            bookingId,
            Math.Abs(redeemedTransaction.Points),
            booking.Status == "Expired" ? "RedeemExpired" : "RedeemRefund",
            LoyaltyEffectTypes.RedeemReturn,
            booking.Status == "Expired"
                ? "Returned redeemed points after booking expiration."
                : "Returned redeemed points after refund.",
            cancellationToken);
    }

    private async Task AddTransactionAsync(
        Guid userId,
        Guid bookingId,
        int points,
        string type,
        string? effectType,
        string description,
        CancellationToken cancellationToken)
    {
        var wallet = await _db.LoyaltyPoints
            .FirstOrDefaultAsync(lp => lp.UserId == userId, cancellationToken);
        if (wallet == null)
        {
            wallet = new LoyaltyPoint { UserId = userId };
            _db.LoyaltyPoints.Add(wallet);
        }

        wallet.Points = Math.Max(0, wallet.Points + points);
        _db.PointTransactions.Add(new PointTransaction
        {
            UserId = userId,
            BookingId = bookingId,
            Points = points,
            Type = type,
            EffectType = effectType,
            BalanceAfter = wallet.Points,
            Description = description
        });
    }
}
