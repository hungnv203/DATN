using AutoMapper;
using MovieBooking.Domain.Common;

namespace MovieBooking.Infrastructure.Mapping;

internal static class EntityDtoMappingExtensions
{
    public static IMappingExpression<TDto, TEntity> IgnoreBaseEntityFromDto<TDto, TEntity>(
        this IMappingExpression<TDto, TEntity> map)
        where TEntity : BaseEntity =>
        map
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore())
            .ForMember(d => d.UpdatedAt, o => o.Ignore());
}
