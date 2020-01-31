using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using SimpleFilterApi.Models;

namespace SimpleFilterApi.Core
{
    public static class WhereFilterBuilderExpression
    {
        public static IQueryable<T> FilterByExpr<T>(this IQueryable<T> source, object filterObject)
        {
            return source.Where(FilterByCore());

            Expression<Func<T, bool>> FilterByCore()
            {
                var type = typeof(T);
                var param = Expression.Parameter(type);
                var filterPropertyInfos = PropertyHelper.GetProperties(filterObject);

                var filterExpression = (
                    from propertyHelper in filterPropertyInfos
                    let val = propertyHelper.GetValueOrDefault(filterObject, null)
                    where val != null
                    let memberExpression = Expression.Property(param, propertyHelper.Name)
                    let filterType = propertyHelper.PropertyInfo.GetCustomAttribute<FilterTypeAttribute>()
                                                   ?.FilterType ?? FilterType.Equals
                    select filterType switch
                    {
                        FilterType.Equals => Expression.Equal(memberExpression, Expression.Constant(val, propertyHelper.PropertyInfo.PropertyType)),
                        FilterType.Greater => Expression.GreaterThanOrEqual(memberExpression,
                                                                            Expression.Constant(val, propertyHelper.PropertyInfo.PropertyType)),
                        FilterType.Lesser => Expression.LessThanOrEqual(memberExpression, Expression.Constant(val, propertyHelper.PropertyInfo.PropertyType)),
                        _                 => throw new ArgumentException("filter is not valid")
                    }).Aggregate<BinaryExpression, Expression>(null,
                                                               (current, compareExpression) =>
                                                                   current != null ? Expression.AndAlso(current, compareExpression) : compareExpression);

                return Expression.Lambda<Func<T, bool>>(filterExpression, param);
            }
        }
    }
}