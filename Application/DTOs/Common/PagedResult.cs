namespace Application.DTOs.Common
{
  public class PagedResult<T>
  {
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public PagedResult() { }

    public PagedResult(List<T> items, int totalCount, int page, int pageSize)
    {
      Items = items;
      TotalCount = totalCount;
      Page = page;
      PageSize = pageSize;
    }
  }
}
