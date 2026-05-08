using MovieBooking.Domain.Common;

namespace MovieBooking.Application.Common.Interfaces;

public interface ICrudService<TEntity, TDto>
    where TEntity : BaseEntity
    where TDto : class, new()
{
    Task<IReadOnlyList<TDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TDto> CreateAsync(TDto dto, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(Guid id, TDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
