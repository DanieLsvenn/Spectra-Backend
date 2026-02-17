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
    public interface IComplaintRequestService
    {
        // Create operations
        Task<ComplaintRequest> CreateComplaintAsync(ComplaintRequest complaint);

        // Read operations
        Task<ComplaintRequest?> GetComplaintByIdAsync(Guid requestId);
        Task<ComplaintRequest?> GetComplaintByIdWithDetailsAsync(Guid requestId);
        Task<List<ComplaintRequest>> GetComplaintsByUserAsync(Guid userId);
        Task<PaginationResult<ComplaintRequest>> GetComplaintsByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10);
        Task<PaginationResult<ComplaintRequest>> GetAllComplaintsAsync(int currentPage = 1, int pageSize = 10);
        Task<PaginationResult<ComplaintRequest>> GetComplaintsByStatusAsync(string status, int currentPage = 1, int pageSize = 10);

        // Update operations
        Task<ComplaintRequest?> UpdateComplaintStatusAsync(Guid requestId, string newStatus, string userRole);
        Task<ComplaintRequest?> UpdateComplaintAsync(Guid requestId, ComplaintRequest updatedComplaint, Guid userId);

        // Validation
        bool IsValidRequestType(string requestType);
        bool IsValidStatus(string status);
        bool CanCustomerModify(ComplaintRequest complaint);
    }

    public class ComplaintRequestService : IComplaintRequestService
    {
        private readonly GenericRepository<ComplaintRequest> _complaintRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<Order> _orderRepository;

        // Request types
        public static class RequestType
        {
            public const string Return = "return";
            public const string Exchange = "exchange";
            public const string Refund = "refund";
            public const string Complaint = "complaint";
            public const string Warranty = "warranty";
        }

        // Complaint statuses
        public static class ComplaintStatus
        {
            public const string Pending = "pending";
            public const string UnderReview = "under_review";
            public const string Approved = "approved";
            public const string Rejected = "rejected";
            public const string InProgress = "in_progress";
            public const string Resolved = "resolved";
            public const string Cancelled = "cancelled";
        }

        private static readonly string[] ValidRequestTypes = 
        { 
            RequestType.Return, 
            RequestType.Exchange, 
            RequestType.Refund, 
            RequestType.Complaint, 
            RequestType.Warranty 
        };

        private static readonly string[] ValidStatuses = 
        { 
            ComplaintStatus.Pending, 
            ComplaintStatus.UnderReview, 
            ComplaintStatus.Approved, 
            ComplaintStatus.Rejected, 
            ComplaintStatus.InProgress, 
            ComplaintStatus.Resolved, 
            ComplaintStatus.Cancelled 
        };

        // Statuses that allow customer modification
        private static readonly string[] ModifiableStatuses = 
        { 
            ComplaintStatus.Pending 
        };

        public ComplaintRequestService(
            GenericRepository<ComplaintRequest> complaintRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<Order> orderRepository)
        {
            _complaintRepository = complaintRepository;
            _orderItemRepository = orderItemRepository;
            _orderRepository = orderRepository;
        }

        #region Create Operations

        public async Task<ComplaintRequest> CreateComplaintAsync(ComplaintRequest complaint)
        {
            complaint.RequestId = Guid.NewGuid();
            complaint.Status = ComplaintStatus.Pending;
            complaint.CreatedAt = DateTime.UtcNow;

            return await _complaintRepository.CreateAsync(complaint);
        }

        #endregion

        #region Read Operations

        public async Task<ComplaintRequest?> GetComplaintByIdAsync(Guid requestId)
        {
            var complaints = await _complaintRepository.SearchAsync(c => c.RequestId == requestId);
            return complaints.FirstOrDefault();
        }

        public async Task<ComplaintRequest?> GetComplaintByIdWithDetailsAsync(Guid requestId)
        {
            var complaints = await _complaintRepository.SearchAsyncInclude(
                c => c.RequestId == requestId,
                c => c.User,
                c => c.OrderItem
            );

            var complaint = complaints.FirstOrDefault();

            if (complaint?.OrderItem != null && complaint.OrderItem.OrderId.HasValue)
            {
                var orders = await _orderRepository.SearchAsync(o => o.OrderId == complaint.OrderItem.OrderId);
                complaint.OrderItem.Order = orders.FirstOrDefault();
            }

            return complaint;
        }

        public async Task<List<ComplaintRequest>> GetComplaintsByUserAsync(Guid userId)
        {
            var complaints = await _complaintRepository.SearchAsyncIncludeOrderBy(
                c => c.UserId == userId,
                orderBy: c => c.CreatedAt,
                ascending: false,
                c => c.OrderItem
            );
            return complaints.ToList();
        }

        public async Task<PaginationResult<ComplaintRequest>> GetComplaintsByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10)
        {
            return await _complaintRepository.SearchWithPagingAsyncIncludeOrderBy(
                c => c.UserId == userId,
                currentPage,
                pageSize,
                orderBy: c => c.CreatedAt,
                ascending: false,
                c => c.OrderItem
            );
        }

        public async Task<PaginationResult<ComplaintRequest>> GetAllComplaintsAsync(int currentPage = 1, int pageSize = 10)
        {
            return await _complaintRepository.SearchWithPagingAsyncIncludeOrderBy(
                c => true,
                currentPage,
                pageSize,
                orderBy: c => c.CreatedAt,
                ascending: false,
                c => c.User,
                c => c.OrderItem
            );
        }

        public async Task<PaginationResult<ComplaintRequest>> GetComplaintsByStatusAsync(string status, int currentPage = 1, int pageSize = 10)
        {
            return await _complaintRepository.SearchWithPagingAsyncIncludeOrderBy(
                c => c.Status != null && c.Status.ToLower() == status.ToLower(),
                currentPage,
                pageSize,
                orderBy: c => c.CreatedAt,
                ascending: false,
                c => c.User,
                c => c.OrderItem
            );
        }

        #endregion

        #region Update Operations

        public async Task<ComplaintRequest?> UpdateComplaintStatusAsync(Guid requestId, string newStatus, string userRole)
        {
            var complaint = await GetComplaintByIdAsync(requestId);

            if (complaint == null)
            {
                return null;
            }

            // Validate status
            if (!IsValidStatus(newStatus))
            {
                return null;
            }

            // Only staff, manager, admin can update status
            var allowedRoles = new[] { "staff", "manager", "admin" };
            if (!allowedRoles.Contains(userRole.ToLower()))
            {
                return null;
            }

            complaint.Status = newStatus.ToLower();
            return await _complaintRepository.UpdateAsync(complaint);
        }

        public async Task<ComplaintRequest?> UpdateComplaintAsync(Guid requestId, ComplaintRequest updatedComplaint, Guid userId)
        {
            var existingComplaint = await GetComplaintByIdAsync(requestId);

            if (existingComplaint == null)
            {
                return null;
            }

            // Verify ownership
            if (existingComplaint.UserId != userId)
            {
                return null;
            }

            // Check if can modify
            if (!CanCustomerModify(existingComplaint))
            {
                return null;
            }

            // Update allowed fields
            if (!string.IsNullOrEmpty(updatedComplaint.RequestType))
                existingComplaint.RequestType = updatedComplaint.RequestType;

            if (!string.IsNullOrEmpty(updatedComplaint.Reason))
                existingComplaint.Reason = updatedComplaint.Reason;

            if (!string.IsNullOrEmpty(updatedComplaint.MediaUrl))
                existingComplaint.MediaUrl = updatedComplaint.MediaUrl;

            return await _complaintRepository.UpdateAsync(existingComplaint);
        }

        #endregion

        #region Validation

        public bool IsValidRequestType(string requestType)
        {
            return ValidRequestTypes.Contains(requestType.ToLower());
        }

        public bool IsValidStatus(string status)
        {
            return ValidStatuses.Contains(status.ToLower());
        }

        public bool CanCustomerModify(ComplaintRequest complaint)
        {
            if (string.IsNullOrEmpty(complaint.Status))
            {
                return true;
            }

            return ModifiableStatuses.Contains(complaint.Status.ToLower());
        }

        #endregion
    }
}
