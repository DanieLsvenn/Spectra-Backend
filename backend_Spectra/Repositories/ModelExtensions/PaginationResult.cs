namespace Repositories.ModelExtensions
{
    public class PaginationResult<T> where T : class
    {
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public List<T> Items { get; set; }
    }
    public class GroupedPaginationResult<TKey, TItem>
    {
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public List<GroupedData<TKey, TItem>> Items { get; set; }
    }

    public class GroupedData<TKey, TItem>
    {
        public TKey Key { get; set; }
        public List<TItem> Items { get; set; }
    }

}
