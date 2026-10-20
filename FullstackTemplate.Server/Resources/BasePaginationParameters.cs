namespace FullstackTemplate.Server.Resources
{
    using System.ComponentModel.DataAnnotations;

    public abstract class BasePaginationParameters
    {
        public const int MaxPageSizeLimit = 500;

        // The offset of the last page, (MaxPageNumber - 1) * MaxPageSizeLimit, must fit in an int
        public const int MaxPageNumber = int.MaxValue / MaxPageSizeLimit;

        public const int DefaultPageSizeValue = 10;

        internal virtual int MaxPageSize { get; } = MaxPageSizeLimit;
        internal virtual int DefaultPageSize { get; set; } = DefaultPageSizeValue;

        // A page starts at 1. A value less than 1 gives a negative OFFSET, and the database rejects it.
        [Range(1, MaxPageNumber)]
        public virtual int PageNumber { get; set; } = 1;

        // The setter limits a large value to MaxPageSize. A value less than 1 gives no rows and no end of pages.
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

        /// <summary>
        /// Sets the page values that the caller gave. A null value keeps the default. Each entrypoint calls this
        /// through the <c>From</c> method of a parameters DTO, so that the defaults and the limits are in one place.
        /// </summary>
        protected void SetPage(int? pageNumber, int? pageSize)
        {
            if (pageNumber is not null)
                PageNumber = pageNumber.Value;
            if (pageSize is not null)
                PageSize = pageSize.Value;
        }
    }
}