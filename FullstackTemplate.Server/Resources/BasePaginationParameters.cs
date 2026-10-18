namespace FullstackTemplate.Server.Resources
{
    using Microsoft.AspNetCore.Mvc;

    public abstract class BasePaginationParameters
    {
        internal virtual int MaxPageSize { get; } = 500;
        internal virtual int DefaultPageSize { get; set; } = 10;

        // The names in the query string are camelCase, the same as the keys of the errors of the problem
        [FromQuery(Name = "pageNumber")]
        public virtual int PageNumber { get; set; } = 1;

        [FromQuery(Name = "pageSize")]
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