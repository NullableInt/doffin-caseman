namespace DoffinCaseman.Web.Services;

public static class PageWindow
{
    // Page numbers to render in a pager: always the first and last page plus
    // `radius` pages either side of the current one. A null entry stands for
    // an ellipsis. A gap of exactly one page is filled with that page rather
    // than an ellipsis, since "..." would take as much room as the number.
    public static IReadOnlyList<int?> Build(int current, int totalPages, int radius = 2)
    {
        totalPages = Math.Max(1, totalPages);
        current = Math.Clamp(current, 1, totalPages);

        var items = new List<int?>();
        var previous = 0;
        for (var page = 1; page <= totalPages; page++)
        {
            var shown = page == 1 || page == totalPages || Math.Abs(page - current) <= radius;
            if (!shown)
                continue;

            if (page - previous == 2)
                items.Add(previous + 1);
            else if (page - previous > 2)
                items.Add(null);

            items.Add(page);
            previous = page;
        }
        return items;
    }
}
