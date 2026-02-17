using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public interface IUserService
    {
        // Read operations
        Task<User?> GetUserByIdAsync(Guid userId);
        Task<User?> GetUserByEmailAsync(string email);
        Task<PaginationResult<User>> GetAllUsersAsync(int currentPage = 1, int pageSize = 10);
        Task<PaginationResult<User>> SearchUsersAsync(string searchTerm, int currentPage = 1, int pageSize = 10);
        Task<PaginationResult<User>> GetUsersByRoleAsync(string role, int currentPage = 1, int pageSize = 10);
        Task<PaginationResult<User>> GetUsersByStatusAsync(string status, int currentPage = 1, int pageSize = 10);

        // Update operations
        Task<User?> UpdateUserAsync(Guid userId, User updatedUser);
        Task<User?> UpdateUserStatusAsync(Guid userId, string status);
        Task<User?> UpdateUserRoleAsync(Guid userId, string role);

        // Create operations (Admin)
        Task<User> CreateUserAsync(User user);

        // Validation
        bool IsValidRole(string role);
        bool IsValidStatus(string status);
    }

    public class UserService : IUserService
    {
        private readonly GenericRepository<User> _userRepository;

        // User roles
        public static class UserRole
        {
            public const string Customer = "customer";
            public const string Staff = "staff";
            public const string Manager = "manager";
            public const string Admin = "admin";
        }

        // User statuses
        public static class UserStatus
        {
            public const string Active = "active";
            public const string Inactive = "inactive";
            public const string Suspended = "suspended";
            public const string Pending = "pending";
        }

        private static readonly string[] ValidRoles = 
        { 
            UserRole.Customer, 
            UserRole.Staff, 
            UserRole.Manager, 
            UserRole.Admin 
        };

        private static readonly string[] ValidStatuses = 
        { 
            UserStatus.Active, 
            UserStatus.Inactive, 
            UserStatus.Suspended, 
            UserStatus.Pending 
        };

        public UserService(GenericRepository<User> userRepository)
        {
            _userRepository = userRepository;
        }

        #region Read Operations

        public async Task<User?> GetUserByIdAsync(Guid userId)
        {
            var users = await _userRepository.SearchAsync(u => u.UserId == userId);
            return users.FirstOrDefault();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var users = await _userRepository.SearchAsync(u => u.Email == email);
            return users.FirstOrDefault();
        }

        public async Task<PaginationResult<User>> GetAllUsersAsync(int currentPage = 1, int pageSize = 10)
        {
            return await _userRepository.SearchWithPagingAsyncIncludeOrderBy(
                u => true,
                currentPage,
                pageSize,
                orderBy: u => u.CreatedAt,
                ascending: false
            );
        }

        public async Task<PaginationResult<User>> SearchUsersAsync(string searchTerm, int currentPage = 1, int pageSize = 10)
        {
            searchTerm = searchTerm.ToLower();

            return await _userRepository.SearchWithPagingAsyncIncludeOrderBy(
                u => (u.FullName != null && u.FullName.ToLower().Contains(searchTerm)) ||
                     (u.Email != null && u.Email.ToLower().Contains(searchTerm)) ||
                     (u.Phone != null && u.Phone.Contains(searchTerm)),
                currentPage,
                pageSize,
                orderBy: u => u.FullName,
                ascending: true
            );
        }

        public async Task<PaginationResult<User>> GetUsersByRoleAsync(string role, int currentPage = 1, int pageSize = 10)
        {
            return await _userRepository.SearchWithPagingAsyncIncludeOrderBy(
                u => u.Role != null && u.Role.ToLower() == role.ToLower(),
                currentPage,
                pageSize,
                orderBy: u => u.CreatedAt,
                ascending: false
            );
        }

        public async Task<PaginationResult<User>> GetUsersByStatusAsync(string status, int currentPage = 1, int pageSize = 10)
        {
            return await _userRepository.SearchWithPagingAsyncIncludeOrderBy(
                u => u.Status != null && u.Status.ToLower() == status.ToLower(),
                currentPage,
                pageSize,
                orderBy: u => u.CreatedAt,
                ascending: false
            );
        }

        #endregion

        #region Update Operations

        public async Task<User?> UpdateUserAsync(Guid userId, User updatedUser)
        {
            var existingUser = await GetUserByIdAsync(userId);

            if (existingUser == null)
            {
                return null;
            }

            // Update allowed fields
            if (!string.IsNullOrEmpty(updatedUser.FullName))
                existingUser.FullName = updatedUser.FullName;

            if (!string.IsNullOrEmpty(updatedUser.Phone))
                existingUser.Phone = updatedUser.Phone;

            if (!string.IsNullOrEmpty(updatedUser.Address))
                existingUser.Address = updatedUser.Address;

            // Email update requires validation (not updating here to avoid duplicates)
            // Role and Status should be updated via specific methods

            return await _userRepository.UpdateAsync(existingUser);
        }

        public async Task<User?> UpdateUserStatusAsync(Guid userId, string status)
        {
            if (!IsValidStatus(status))
            {
                return null;
            }

            var user = await GetUserByIdAsync(userId);

            if (user == null)
            {
                return null;
            }

            user.Status = status.ToLower();
            return await _userRepository.UpdateAsync(user);
        }

        public async Task<User?> UpdateUserRoleAsync(Guid userId, string role)
        {
            if (!IsValidRole(role))
            {
                return null;
            }

            var user = await GetUserByIdAsync(userId);

            if (user == null)
            {
                return null;
            }

            user.Role = role.ToLower();
            return await _userRepository.UpdateAsync(user);
        }

        #endregion

        #region Create Operations

        public async Task<User> CreateUserAsync(User user)
        {
            user.UserId = Guid.NewGuid();
            user.CreatedAt = DateTime.UtcNow;

            // Set defaults
            if (string.IsNullOrEmpty(user.Status))
            {
                user.Status = UserStatus.Active;
            }

            if (string.IsNullOrEmpty(user.Role))
            {
                user.Role = UserRole.Customer;
            }

            return await _userRepository.CreateAsync(user);
        }

        #endregion

        #region Validation

        public bool IsValidRole(string role)
        {
            return ValidRoles.Contains(role.ToLower());
        }

        public bool IsValidStatus(string status)
        {
            return ValidStatuses.Contains(status.ToLower());
        }

        #endregion
    }
}
