using System;

namespace SimpleFilterApi.Models
{
    public class PersonFilter
    {
        [FilterType(FilterType.Greater)]
        public int Age { get; set; }

        public Guid? Id { get; set; }

        [FilterType(FilterType.Equals)]
        public string Name { get; set; }
    }

    public enum FilterType
    {
        Equals,
        Greater,
        Lesser
    }

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    internal sealed class FilterTypeAttribute : Attribute
    {
        public FilterTypeAttribute(FilterType filterType)
        {
            this.FilterType = filterType;
        }

        public FilterType FilterType { get; }
    }
}