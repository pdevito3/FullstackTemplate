namespace FullstackTemplate.Server.Resources
{
    using System.ComponentModel.DataAnnotations;
    using Microsoft.AspNetCore.Mvc;

    public abstract class BasePaginationParameters
    {
        internal virtual int MaxPageSize { get; } = 500;
        internal virtual int DefaultPageSize { get; set; } = 10;

        // The names in the query string are camelCase, the same as the keys of the errors of the problem
        // A page starts at 1. A value less than 1 gives a negative OFFSET, and the database rejects it.
        [FromQuery(Name = "pageNumber")]
        [Range(1, int.MaxValue)]
        public virtual int PageNumber { get; set; } = 1;

        // The setter limits a large value to MaxPageSize. A value less than 1 gives no rows and no end of pages.
        [FromQuery(Name = "pageSize")]
        [Range(1, int.MaxValue)]
        public int PageSize
        {
            get
            {
                return DefaultPageSize;
            }
            set
            {
                DefaultPageSize = value > MaxPageSize ? MaxPageSize : value;
            }
        }
    }
}