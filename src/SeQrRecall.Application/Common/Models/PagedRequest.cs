namespace SeQrRecall.Application.Common.Models;

/// <summary>
/// Shared pagination query parameters. Default page size is 20; maximum is 100.
/// </summary>
public sealed class PagedRequest
{
    public const int DefaultPageNumber = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private int _pageNumber = DefaultPageNumber;
    private int _pageSize = DefaultPageSize;

    public int PageNumber
    {
        get => _pageNumber;
        set => _pageNumber = value < 1 ? DefaultPageNumber : value;
    }

    public int PageSize
    {
        get => _pageSize;
        set
        {
            if (value < 1)
            {
                _pageSize = DefaultPageSize;
                return;
            }

            _pageSize = Math.Min(value, MaxPageSize);
        }
    }

    public string? Search { get; set; }
}
