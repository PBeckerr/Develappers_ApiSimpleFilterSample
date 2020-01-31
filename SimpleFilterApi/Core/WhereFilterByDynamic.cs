using System;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Reflection;
using SimpleFilterApi.Models;

namespace SimpleFilterApi.Core
{
    public static class WhereFilterByDynamic
    {
        public static IQueryable<T> FilterByDynamic<T>(this IQueryable<T> source, object filterObject)
        {
            var properties = PropertyHelper.GetProperties(filterObject);

            foreach (var propertyHelper in properties)
            {
                var filterType = propertyHelper.PropertyInfo.GetCustomAttribute<FilterTypeAttribute>()
                                               ?.FilterType ?? FilterType.Equals;
                var value = propertyHelper.GetValueOrDefault(filterObject, null);

                if (value == null)
                {
                    continue;
                }

                source = source.Where(filterType switch
                                      {
                                          FilterType.Equals  => $"{propertyHelper.Name} == @0",
                                          FilterType.Greater => $"{propertyHelper.Name} >= @0",
                                          FilterType.Lesser  => $"{propertyHelper.Name} <= @0",
                                          _                  => throw new ArgumentException("filter is not valid")
                                      },
                                      value);
            }

            return source;
        }
    }
}