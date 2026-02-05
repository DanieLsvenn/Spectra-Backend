using Microsoft.EntityFrameworkCore;
using Repositories.ModelExtensions;
using Repositories.Models;
using System.Linq.Expressions;

namespace Repositories.Basic
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly Context _context;

        public GenericRepository()
        {
            _context ??= new Context();
        }

        public GenericRepository(Context context)
        {
            _context = context;
        }

        public List<T> GetAll()
        {
            // Use fresh context to avoid caching issues
            using (var context = new Context())
            {
                return context.Set<T>().ToList();
            }
        }

        public List<T> GetAllInclude(params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return query.ToList();

        }

        public List<T> GetAllIncludeOrderBy(
            Expression<Func<T, object>> orderBy,
            bool ascending = true,
            params Expression<Func<T, object>>[] includes
            )
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            query = ascending
                ? query.OrderBy(orderBy)
                : query.OrderByDescending(orderBy);
            return query.ToList();
        }

        public async Task<List<T>> GetAllAsync()
        {
            return await _context.Set<T>().ToListAsync();
        }

        public async Task<List<T>> GetAllAsyncInclude(params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.ToListAsync();
        }

        public async Task<List<T>> GetAllAsyncIncludeOrderBy(
                    Expression<Func<T, object>> orderBy,
                    bool ascending = true,
                    params Expression<Func<T, object>>[] includes
                    )
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            query = ascending
                ? query.OrderBy(orderBy)
                : query.OrderByDescending(orderBy);
            return await query.ToListAsync();
        }

        public async Task<List<T>> GetAllAsyncOrderBy(
            Expression<Func<T, object>> orderBy,
            bool ascending = true
            )
        {
            IQueryable<T> query = _context.Set<T>();
            query = ascending ?
                query.OrderBy(orderBy)
                : query.OrderByDescending(orderBy);
            return await query.ToListAsync();
        }

        public void Create(T entity)
        {
            _context.Add(entity);
            _context.SaveChanges();
        }

        public async Task<T> CreateAsync(T entity)
        {
            _context.Add(entity);
            await _context.SaveChangesAsync();
            return entity;
        }

        public void Update(T entity)
        {
            _context.ChangeTracker.Clear();
            var tracker = _context.Attach(entity);
            tracker.State = EntityState.Modified;
            _context.SaveChanges();

        }

        public async Task<T> UpdateAsync(T entity)
        {
            _context.ChangeTracker.Clear();
            var tracker = _context.Attach(entity);
            tracker.State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return entity;
        }

        public bool Delete(T entity)
        {
            _context.Remove(entity);
            _context.SaveChanges();
            return true;
        }

        public async Task<bool> DeleteAsync(T entity)
        {
            _context.Remove(entity);
            await _context.SaveChangesAsync();
            return true;
        }

        public T? GetById<TKey>(TKey id)
        {
            return _context.Set<T>().Find(id);
        }

        public T? GetByIdInclude<TKey>(
            TKey id,
            params Expression<Func<T, object>>[] includes
            )
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            var keyName = _context.Model.FindEntityType(typeof(T)).FindPrimaryKey().Properties[0].Name;
            return query.FirstOrDefault(e => EF.Property<TKey>(e, keyName).Equals(id));
        }

        public async Task<T?> GetByIdAsync<TKey>(TKey id)
        {
            return await _context.Set<T>().FindAsync(id);
        }

        public async Task<T?> GetByIdAsyncInclude<TKey>(
            TKey id,
            params Expression<Func<T, object>>[] includes
            )
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            var keyName = _context.Model.FindEntityType(typeof(T)).FindPrimaryKey().Properties[0].Name;
            return await query.FirstOrDefaultAsync(e => EF.Property<TKey>(e, keyName).Equals(id));
        }

        public IQueryable<T> GetSet()
        {
            return _context.Set<T>();
        }

        public IEnumerable<T> Search(Expression<Func<T, bool>> predicate)
        {
            return _context.Set<T>().Where(predicate).ToList();
        }

        public IEnumerable<T> SearchInclude(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return query.Where(predicate).ToList();
        }

        public IEnumerable<T> SearchIncludeOrderBy(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, object>> orderBy,
            bool ascending = true,
            params Expression<Func<T, object>>[] includes
            )
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return query.ToList();
        }

        public async Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>> predicate)
        {
            return await _context.Set<T>().Where(predicate).ToListAsync();
        }

        public async Task<IEnumerable<T>> SearchAsyncInclude(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            query = query.Where(predicate);
            return await query.ToListAsync();
        }

        public async Task<IEnumerable<T>> SearchAsyncIncludeOrderBy(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, object>> orderBy,
            bool ascending = true,
            params Expression<Func<T, object>>[] includes
        )
        {
            IQueryable<T> query = _context.Set<T>();
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await query.ToListAsync();
        }

        public async Task<IEnumerable<T>> SearchAsyncOrderBy(
            Expression<Func<T, bool>> predicate,
            Expression<Func<T, object>> orderBy,
            bool ascending = true
            )
        {
            IQueryable<T> query = _context.Set<T>();
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await query.ToListAsync();
        }

        public async Task<PaginationResult<T>> SearchWithPagingAsync(
                    Expression<Func<T, bool>> predicate,
                    int currentPage = 1,
                    int pageSize = 10)
        {
            IQueryable<T> query = _context.Set<T>().Where(predicate);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            var items = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginationResult<T>
            {
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = currentPage,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<PaginationResult<T>> SearchWithPagingAsyncIncludeOrderBy(
            Expression<Func<T, bool>> predicate,
            int currentPage = 1,
            int pageSize = 10,
            Expression<Func<T, object>>? orderBy = null,
            bool ascending = true,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = _context.Set<T>();

            // Apply includes
            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            // Apply predicate
            query = query.Where(predicate);

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);

            // Apply ordering if specified
            if (orderBy != null)
            {
                query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            }

            var items = await query
                .Skip((currentPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginationResult<T>
            {
                TotalItems = totalItems,
                TotalPages = totalPages,
                CurrentPage = currentPage,
                PageSize = pageSize,
                Items = items
            };
        }

        public async Task<int> GetMaxId()
        {
            var entities = await _context.Set<T>().ToListAsync();

            if (!entities.Any())
                return 0;

            var keyProperty = _context.Model.FindEntityType(typeof(T)).FindPrimaryKey()
                .Properties[0];

            return entities
                .Select(e => (int)keyProperty.PropertyInfo.GetValue(e)).Max();
        }
    }
}