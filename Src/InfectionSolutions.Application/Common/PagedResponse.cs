namespace InfectionSolutions.Application.Common;

public sealed record PageRequest
{
    private const int MaxPageSize = 100;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;

    public int CurrentPage => Page > 0 ? Page : 1;

    public int Take => PageSize is > MaxPageSize or <= 0 ? MaxPageSize : PageSize;

    public int Skip => (CurrentPage - 1) * Take;
}

public sealed record PagedResult<TItem>
{
    public required IReadOnlyList<TItem> Items { get; init; }

    public int TotalCount { get; init; }
}

public sealed record PagedResponse<TItem>
{
    public required IReadOnlyList<TItem> Items { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public static PagedResponse<TItem> From<TSource>(PagedResult<TSource> result, PageRequest page, Func<TSource, TItem> map) => new()
    {
        Items = result.Items.Select(map).ToList(),
        Page = page.CurrentPage,
        PageSize = page.Take,
        TotalCount = result.TotalCount
    };
}
