using AutoMapper;
using MovieBooking.Application.Common.DTOs;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Entities;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal sealed class NotificationService : INotificationService
{
    private readonly EntityCrudOperations<Notification, NotificationDto> _operations;

    public NotificationService(AppDbContext dbContext, IMapper mapper)
    {
        _operations = new EntityCrudOperations<Notification, NotificationDto>(dbContext, mapper);
    }

    public Task<IReadOnlyList<NotificationDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _operations.GetAllAsync(cancellationToken);

    public Task<NotificationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.GetByIdAsync(id, cancellationToken);

    public Task<NotificationDto> CreateAsync(NotificationDto dto, CancellationToken cancellationToken = default) =>
        _operations.CreateAsync(dto, cancellationToken);

    public Task<bool> UpdateAsync(Guid id, NotificationDto dto, CancellationToken cancellationToken = default) =>
        _operations.UpdateAsync(id, dto, cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _operations.DeleteAsync(id, cancellationToken);
}

