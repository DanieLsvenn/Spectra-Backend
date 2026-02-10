using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface IAccountService
    {
        // Authentication
        Task<User?> GetUser(string email, string passwordHash);
        Task<User?> GetUserByEmail(string email);
        Task<User?> GetUserByUsername(string fullName);

        // Get All
        Task<List<User>> GetAllAsync();
        Task<List<User>> GetAllAsyncInclude();
        Task<List<User>> GetAllAsyncIncludeOrderBy();

        // Get By Id
        Task<User?> GetByIdAsync(int id);
        Task<User?> GetByIdAsyncInclude(int id);

        // Create
        Task<User> CreateAsync(User entity);

        // Update
        Task<User> UpdateAsync(User entity);

        // Delete
        Task<bool> DeleteAsync(int id);

        // Search
        Task<IEnumerable<User>> SearchAsyncIncludeOrderBy(Expression<Func<User, bool>> predicate);
        Task<PaginationResult<User>> SearchWithPagingAsyncIncludeOrderBy(
            Expression<Func<User, bool>> predicate,
            int currentPage = 1,
            int pageSize = 10);

        // Utilities
        Task<int> GetMaxId();
    }

    public class AccountService : IAccountService
    {
        private readonly GenericRepository<User> _repository;

        public AccountService(GenericRepository<User> repository)
        {
            _repository = repository;
        }

        // Authentication
        public async Task<User?> GetUser(string email, string passwordHash)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(passwordHash))
            {
                return null;
            }

            var userAccount = await _repository
                .GetSet()
                .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == passwordHash);

            return userAccount;
        }

        public async Task<User?> GetUserByEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return null;
            }

            return await _repository
                .GetSet()
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetUserByUsername(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            return await _repository
                .GetSet()
                .FirstOrDefaultAsync(u => u.FullName == fullName);
        }

        // Get All
        public async Task<List<User>> GetAllAsync()
        {
            return await _repository.GetAllAsync();
        }

        public async Task<List<User>> GetAllAsyncInclude()
        {
            return await _repository.GetAllAsyncInclude(
                u => u.Orders,
                u => u.Prescriptions
            );
        }

        public async Task<List<User>> GetAllAsyncIncludeOrderBy()
        {
            return await _repository.GetAllAsyncIncludeOrderBy(
                orderBy: u => u.FullName,
                ascending: true,
                u => u.Orders,
                u => u.Prescriptions
            );
        }

        // Get By Id
        public async Task<User?> GetByIdAsync(int id)
        {
            return await _repository.GetByIdAsync(id);
        }

        public async Task<User?> GetByIdAsyncInclude(int id)
        {
            return await _repository.GetByIdAsyncInclude(
                id,
                u => u.Orders,
                u => u.Prescriptions
            );
        }

        // Create
        public async Task<User> CreateAsync(User entity)
        {
            return await _repository.CreateAsync(entity);
        }

        // Update
        public async Task<User> UpdateAsync(User entity)
        {
            return await _repository.UpdateAsync(entity);
        }

        // Delete
        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _repository.GetByIdAsync(id);
            if (entity == null)
                return false;
            return await _repository.DeleteAsync(entity);
        }

        // Search
        public async Task<IEnumerable<User>> SearchAsyncIncludeOrderBy(Expression<Func<User, bool>> predicate)
        {
            return await _repository.SearchAsyncIncludeOrderBy(
                predicate,
                orderBy: u => u.FullName,
                ascending: true,
                u => u.Orders,
                u => u.Prescriptions
            );
        }

        public async Task<PaginationResult<User>> SearchWithPagingAsyncIncludeOrderBy(
            Expression<Func<User, bool>> predicate,
            int currentPage = 1,
            int pageSize = 10)
        {
            return await _repository.SearchWithPagingAsyncIncludeOrderBy(
                predicate,
                currentPage,
                pageSize,
                orderBy: u => u.FullName,
                ascending: true,
                u => u.Orders,
                u => u.Prescriptions
            );
        }

        // Utilities
        public async Task<int> GetMaxId()
        {
            return await _repository.GetMaxId();
        }
    }
}
