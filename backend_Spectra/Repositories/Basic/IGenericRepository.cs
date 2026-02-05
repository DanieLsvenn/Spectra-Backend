using Repositories.ModelExtensions;
using System.Linq.Expressions;

namespace Repositories.Basic
{
    public interface IGenericRepository<T> where T : class
    {
        // --- Get ---
        public List<T> GetAll();
        public List<T> GetAllInclude(params Expression<Func<T, object>>[] includes);
        public List<T> GetAllIncludeOrderBy(Expression<Func<T, object>> orderBy, bool ascending = true, params Expression<Func<T, object>>[] includes);
        public Task<List<T>> GetAllAsync();
        public Task<List<T>> GetAllAsyncInclude(params Expression<Func<T, object>>[] includes);
        public Task<List<T>> GetAllAsyncIncludeOrderBy(Expression<Func<T, object>> orderBy, bool ascending = true, params Expression<Func<T, object>>[] includes);
        public Task<List<T>> GetAllAsyncOrderBy(Expression<Func<T, object>> orderBy, bool ascending = true);
        public IQueryable<T> GetSet();
        public T? GetById<TKey>(TKey id);
        public T? GetByIdInclude<TKey>(TKey id, params Expression<Func<T, object>>[] includes);
        public Task<T?> GetByIdAsync<TKey>(TKey id);
        public Task<T?> GetByIdAsyncInclude<TKey>(TKey id, params Expression<Func<T, object>>[] includes);

        // --- Search / Filter ---
        public IEnumerable<T> Search(Expression<Func<T, bool>> predicate);
        public IEnumerable<T> SearchInclude(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);
        public IEnumerable<T> SearchIncludeOrderBy(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> orderBy, bool ascending = true, params Expression<Func<T, object>>[] includes);
        public Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>> predicate);
        public Task<IEnumerable<T>> SearchAsyncInclude(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);
        public Task<IEnumerable<T>> SearchAsyncIncludeOrderBy(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> orderBy, bool ascending = true, params Expression<Func<T, object>>[] includes);
        public Task<IEnumerable<T>> SearchAsyncOrderBy(Expression<Func<T, bool>> predicate, Expression<Func<T, object>> orderBy, bool ascending = true);

        // --- Paging (assumes PaginationResult<T> exists in your project) ---
        public Task<PaginationResult<T>> SearchWithPagingAsync(Expression<Func<T, bool>> predicate, int currentPage = 1, int pageSize = 10);

        public Task<PaginationResult<T>> SearchWithPagingAsyncIncludeOrderBy(Expression<Func<T, bool>> predicate, int currentPage = 1, int pageSize = 10, Expression<Func<T, object>>? orderBy = null, bool ascending = true, params Expression<Func<T, object>>[] includes);

        // --- Create / Update / Delete ---
        public void Create(T entity);
        public Task<T> CreateAsync(T entity);

        public void Update(T entity);
        public Task<T> UpdateAsync(T entity);

        public bool Delete(T entity);
        public Task<bool> DeleteAsync(T entity);

        // --- Utilities ---
        public Task<int> GetMaxId();
    }
}
