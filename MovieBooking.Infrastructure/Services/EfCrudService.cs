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

    public virtual async Task<IReadOnlyList<TDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet
            .AsNoTracking()
            .Take(200)
            .ToListAsync(cancellationToken);
        return entities.Select(e => _mapper.Map<TDto>(e)).ToList();
    }

    public virtual async Task<TDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync([id], cancellationToken);
        return entity is null ? null : _mapper.Map<TDto>(entity);
    }

    public virtual async Task<TDto> CreateAsync(TDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new TEntity();
        _mapper.Map(dto, entity);
        await ThrowIfDuplicateUniqueIndexAsync(entity, null, cancellationToken);
        await _dbSet.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return _mapper.Map<TDto>(entity);
    }

    public virtual async Task<bool> UpdateAsync(Guid id, TDto dto, CancellationToken cancellationToken = default)
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

    public virtual async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
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
        if (entityType == null)
        {
            return;
        }

        var uniqueIndexes = entityType.GetIndexes().Where(index => index.IsUnique);
        foreach (var index in uniqueIndexes)
        {
            var values = index.Properties
                .Select(property => new
                {
                    Property = property,
                    Value = property.PropertyInfo?.GetValue(entity)
                })
                .ToList();

            if (values.Any(item => IsEmptyUniqueValue(item.Value)))
            {
                continue;
            }

            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(TEntity), "entity");
            System.Linq.Expressions.Expression? predicate = null;

            if (excludeId.HasValue)
            {
                var idProperty = System.Linq.Expressions.Expression.Property(parameter, nameof(BaseEntity.Id));
                var idValue = System.Linq.Expressions.Expression.Constant(excludeId.Value);
                predicate = System.Linq.Expressions.Expression.NotEqual(idProperty, idValue);
            }

            foreach (var item in values)
            {
                if (item.Property.PropertyInfo == null)
                {
                    continue;
                }

                var propertyAccess = System.Linq.Expressions.Expression.Property(parameter, item.Property.PropertyInfo);
                var expectedValue = System.Linq.Expressions.Expression.Constant(item.Value, propertyAccess.Type);
                var equalExpression = System.Linq.Expressions.Expression.Equal(propertyAccess, expectedValue);
                predicate = predicate == null
                    ? equalExpression
                    : System.Linq.Expressions.Expression.AndAlso(predicate, equalExpression);
            }

            if (predicate == null)
            {
                continue;
            }

            var lambda = System.Linq.Expressions.Expression.Lambda<Func<TEntity, bool>>(predicate, parameter);
            if (await _dbSet.AsNoTracking().AnyAsync(lambda, cancellationToken))
            {
                var properties = string.Join(", ", index.Properties.Select(property => property.Name));
                throw new InvalidOperationException($"{typeof(TEntity).Name} already exists with the same {properties}.");
            }
        }
    }

    private static bool IsEmptyUniqueValue(object? value)
    {
        return value == null ||
               value is string text && string.IsNullOrWhiteSpace(text) ||
               value is Guid guid && guid == Guid.Empty;
    }
}
