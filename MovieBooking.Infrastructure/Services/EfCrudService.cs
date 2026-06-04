using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Common;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class EfCrudService<TEntity, TDto> : ICrudService<TEntity, TDto>
    where TEntity : BaseEntity, new()
    where TDto : class, new()
{
    private readonly AppDbContext _dbContext;
    private readonly DbSet<TEntity> _dbSet;
    private readonly IMapper _mapper;

    public EfCrudService(AppDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<TEntity>();
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<TDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(e => _mapper.Map<TDto>(e)).ToList();
    }

    public async Task<TDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync([id], cancellationToken);
        return entity is null ? null : _mapper.Map<TDto>(entity);
    }

    public async Task<TDto> CreateAsync(TDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new TEntity();
        _mapper.Map(dto, entity);
        await _dbSet.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<TDto>(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, TDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _mapper.Map(dto, entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        _dbSet.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
