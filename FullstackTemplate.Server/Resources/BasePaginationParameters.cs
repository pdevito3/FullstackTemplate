namespace FullstackTemplate.Server.Resources
{
    using System.ComponentModel.DataAnnotations;
    using Microsoft.AspNetCore.Mvc;

    public abstract class BasePaginationParameters
    {
        public const int MaxPageSizeLimit = 500;

        // The offset of the last page, (MaxPageNumber - 1) * MaxPageSizeLimit, must fit in an int
        public const int MaxPageNumber = int.MaxValue / MaxPageSizeLimit;

        internal virtual int MaxPageSize { get; } = MaxPageSizeLimit;
        internal virtual int DefaultPageSize { get; set; } = 10;

        // The names in the query string are camelCase, the same as the keys of the errors of the problem
        // A page starts at 1. A value less than 1 gives a negative OFFSET, and the database rejects it.
        [FromQuery(Name = "pageNumber")]
        [Range(1, MaxPageNumber)]
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