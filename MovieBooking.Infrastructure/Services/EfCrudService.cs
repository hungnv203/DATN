using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Application.Common.Interfaces;
using MovieBooking.Domain.Common;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

public class EfCrudService<TEntity, TDto> : ICrudService<TEntity, TDto>
    where TEntity : BaseEntity, new()
    where TDto : class, new()
{
    private static readonly HashSet<Type> SimpleTypes =
    [
        typeof(string), typeof(Guid), typeof(Guid?), typeof(bool), typeof(bool?),
        typeof(int), typeof(int?), typeof(long), typeof(long?), typeof(decimal), typeof(decimal?),
        typeof(double), typeof(double?), typeof(float), typeof(float?),
        typeof(DateTime), typeof(DateTime?), typeof(DateTimeOffset), typeof(DateTimeOffset?)
    ];

    private readonly AppDbContext _dbContext;
    private readonly DbSet<TEntity> _dbSet;

    public EfCrudService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
        _dbSet = dbContext.Set<TEntity>();
    }

    public async Task<IReadOnlyList<TDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(MapToDto).ToList();
    }

    public async Task<TDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync([id], cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task<TDto> CreateAsync(TDto dto, CancellationToken cancellationToken = default)
    {
        var entity = new TEntity();
        CopyScalarProperties(dto, entity, skipId: true);
        await _dbSet.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(entity);
    }

    public async Task<bool> UpdateAsync(Guid id, TDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await _dbSet.FindAsync([id], cancellationToken);
        if (entity is null)
        {
            return false;
        }

        CopyScalarProperties(dto, entity, skipId: true);
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

    private static TDto MapToDto(TEntity entity)
    {
        var dto = new TDto();
        CopyScalarProperties(entity, dto, skipId: false);
        return dto;
    }

    private static void CopyScalarProperties(object source, object destination, bool skipId)
    {
        var sourceProperties = source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && IsSimpleType(p.PropertyType))
            .ToDictionary(p => p.Name, p => p);

        var destinationProperties = destination.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && IsSimpleType(p.PropertyType));

        foreach (var destinationProperty in destinationProperties)
        {
            if (skipId && destinationProperty.Name == "Id")
            {
                continue;
            }

            if (sourceProperties.TryGetValue(destinationProperty.Name, out var sourceProperty))
            {
                destinationProperty.SetValue(destination, sourceProperty.GetValue(source));
            }
        }
    }

    private static bool IsSimpleType(Type type)
    {
        var unwrappedType = Nullable.GetUnderlyingType(type) ?? type;
        return SimpleTypes.Contains(type) || SimpleTypes.Contains(unwrappedType) || unwrappedType.IsEnum;
    }
}
