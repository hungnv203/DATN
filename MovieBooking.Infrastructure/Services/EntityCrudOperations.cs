using System.Linq.Expressions;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Domain.Common;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

/// <summary>
/// Shared EF Core operations used through composition by entity services.
/// This type is an implementation detail and is never exposed to controllers.
/// </summary>
internal sealed class EntityCrudOperations<TEntity, TDto>
    where TEntity : BaseEntity, new()
    where TDto : class, new()
{
    private readonly AppDbContext _dbContext;
    private readonly DbSet<TEntity> _dbSet;
    private readonly IMapper _mapper;

    public EntityCrudOperations(AppDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<TEntity>();
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<TDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.AsNoTracking().Take(200).ToListAsync(cancellationToken);
        return entities.Select(entity => _mapper.Map<TDto>(entity)).ToList();
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
        await ThrowIfDuplicateUniqueIndexAsync(entity, null, cancellationToken);
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
        await ThrowIfDuplicateUniqueIndexAsync(entity, id, cancellationToken);
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

    private async Task ThrowIfDuplicateUniqueIndexAsync(
        TEntity entity,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(TEntity));
        if (entityType is null)
        {
            return;
        }

        foreach (var index in entityType.GetIndexes().Where(index => index.IsUnique))
        {
            var values = index.Properties
                .Select(property => new { Property = property, Value = property.PropertyInfo?.GetValue(entity) })
                .ToList();

            if (values.Any(item => IsEmptyUniqueValue(item.Value)))
            {
                continue;
            }

            var parameter = Expression.Parameter(typeof(TEntity), "entity");
            Expression? predicate = null;

            if (excludeId.HasValue)
            {
                predicate = Expression.NotEqual(
                    Expression.Property(parameter, nameof(BaseEntity.Id)),
                    Expression.Constant(excludeId.Value));
            }

            foreach (var item in values.Where(item => item.Property.PropertyInfo is not null))
            {
                var propertyAccess = Expression.Property(parameter, item.Property.PropertyInfo!);
                var equality = Expression.Equal(
                    propertyAccess,
                    Expression.Constant(item.Value, propertyAccess.Type));
                predicate = predicate is null ? equality : Expression.AndAlso(predicate, equality);
            }

            if (predicate is not null && await _dbSet.AsNoTracking().AnyAsync(
                    Expression.Lambda<Func<TEntity, bool>>(predicate, parameter),
                    cancellationToken))
            {
                var properties = string.Join(", ", index.Properties.Select(property => property.Name));
                throw new InvalidOperationException(
                    $"{typeof(TEntity).Name} already exists with the same {properties}.");
            }
        }
    }

    private static bool IsEmptyUniqueValue(object? value) =>
        value is null ||
        value is string text && string.IsNullOrWhiteSpace(text) ||
        value is Guid guid && guid == Guid.Empty;
}

