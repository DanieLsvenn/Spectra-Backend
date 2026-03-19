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
        Task<ComplaintRequest?> UpdateComplaintStatusAsync(Guid requestId, string newStatus, string userRole, string? staffNote = null);
        Task<ComplaintRequest?> UpdateComplaintAsync(Guid requestId, ComplaintRequest updatedComplaint, Guid userId);
        Task<ComplaintRequest?> LinkExchangeOrderAsync(Guid requestId, Guid exchangeOrderId);
        Task<ComplaintRequest?> ProcessRefundAsync(Guid requestId, double refundAmount);
        Task<ComplaintRequest?> SetReturnTrackingAsync(Guid requestId, string trackingNumber);

        // Validation
        bool IsValidRequestType(string requestType);
        bool IsValidStatus(string status);
        bool CanCustomerModify(ComplaintRequest complaint);
        Task<(bool IsValid, string? Error)> ValidateOrderItemOwnershipAsync(Guid orderItemId, Guid userId);
        bool IsValidStatusTransition(string currentStatus, string newStatus);
        Task<ComplaintRequest?> CancelComplaintByCustomerAsync(Guid requestId, Guid userId);
    }

    public class ComplaintRequestService : IComplaintRequestService
    {
        private readonly GenericRepository<ComplaintRequest> _complaintRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<Frame> _frameRepository;
        private readonly GenericRepository<FrameMedium> _frameMediaRepository;

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

        // Valid status transitions
        private static readonly Dictionary<string, string[]> ValidStatusTransitions = new()
        {
            { ComplaintStatus.Pending, new[] { ComplaintStatus.UnderReview, ComplaintStatus.Cancelled } },
            { ComplaintStatus.UnderReview, new[] { ComplaintStatus.Approved, ComplaintStatus.Rejected } },
            { ComplaintStatus.Approved, new[] { ComplaintStatus.InProgress, ComplaintStatus.Cancelled } },
            { ComplaintStatus.Rejected, Array.Empty<string>() },
            { ComplaintStatus.InProgress, new[] { ComplaintStatus.Resolved, ComplaintStatus.Cancelled } },
            { ComplaintStatus.Resolved, Array.Empty<string>() },
            { ComplaintStatus.Cancelled, Array.Empty<string>() }
        };

        public ComplaintRequestService(
            GenericRepository<ComplaintRequest> complaintRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<Order> orderRepository,
            GenericRepository<Frame> frameRepository,
            GenericRepository<FrameMedium> frameMediaRepository)
        {
            _complaintRepository = complaintRepository;
            _orderItemRepository = orderItemRepository;
            _orderRepository = orderRepository;
            _frameRepository = frameRepository;
            _frameMediaRepository = frameMediaRepository;
        }

        #region Create Operations

        public async Task<ComplaintRequest> CreateComplaintAsync(ComplaintRequest complaint)
        {
            complaint.RequestId = Guid.NewGuid();
            complaint.Status = ComplaintStatus.Pending;
            complaint.CreatedAt = TimeHelper.Now;

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

            // Load exchange order if linked
            if (complaint?.ExchangeOrderId.HasValue == true)
            {
                var exchangeOrders = await _orderRepository.SearchAsync(o => o.OrderId == complaint.ExchangeOrderId);
                complaint.ExchangeOrder = exchangeOrders.FirstOrDefault();
            }

            // Load Frame details for the order item
            if (complaint?.OrderItem?.FrameId.HasValue == true)
            {
                var frames = await _frameRepository.SearchAsync(f => f.FrameId == complaint.OrderItem.FrameId);
                complaint.OrderItem.Frame = frames.FirstOrDefault();
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

        public async Task<ComplaintRequest?> UpdateComplaintStatusAsync(Guid requestId, string newStatus, string userRole, string? staffNote = null)
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

            // Enforce valid status transitions
            var currentStatus = complaint.Status?.ToLower() ?? ComplaintStatus.Pending;
            if (!IsValidStatusTransition(currentStatus, newStatus.ToLower()))
            {
                return null;
            }

            complaint.Status = newStatus.ToLower();

            if (!string.IsNullOrWhiteSpace(staffNote))
            {
                complaint.StaffNote = staffNote;
            }

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

        public async Task<ComplaintRequest?> LinkExchangeOrderAsync(Guid requestId, Guid exchangeOrderId)
        {
            var complaint = await GetComplaintByIdAsync(requestId);

            if (complaint == null)
            {
                return null;
            }

            // Only exchange-type complaints can be linked
            if (complaint.RequestType?.ToLower() != RequestType.Exchange)
            {
                return null;
            }

            // Verify the exchange order exists
            var orders = await _orderRepository.SearchAsync(o => o.OrderId == exchangeOrderId);
            if (!orders.Any())
            {
                return null;
            }

            complaint.ExchangeOrderId = exchangeOrderId;
            return await _complaintRepository.UpdateAsync(complaint);
        }

        public async Task<ComplaintRequest?> ProcessRefundAsync(Guid requestId, double refundAmount)
        {
            var complaint = await GetComplaintByIdAsync(requestId);
            if (complaint == null) return null;

            var allowedTypes = new[] { RequestType.Return, RequestType.Refund };
            if (!allowedTypes.Contains(complaint.RequestType?.ToLower()))
                return null;

            var allowedStatuses = new[] { ComplaintStatus.Approved, ComplaintStatus.InProgress };
            if (!allowedStatuses.Contains(complaint.Status?.ToLower()))
                return null;

            complaint.RefundAmount = refundAmount;
            complaint.RefundedAt = TimeHelper.Now;
            return await _complaintRepository.UpdateAsync(complaint);
        }

        public async Task<ComplaintRequest?> SetReturnTrackingAsync(Guid requestId, string trackingNumber)
        {
            var complaint = await GetComplaintByIdAsync(requestId);
            if (complaint == null) return null;

            var allowedTypes = new[] { RequestType.Return, RequestType.Exchange, RequestType.Warranty };
            if (!allowedTypes.Contains(complaint.RequestType?.ToLower()))
                return null;

            complaint.ReturnTrackingNumber = trackingNumber;
            return await _complaintRepository.UpdateAsync(complaint);
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

        public async Task<ComplaintRequest?> CancelComplaintByCustomerAsync(Guid requestId, Guid userId)
        {
            var complaint = await GetComplaintByIdAsync(requestId);
            if (complaint == null) return null;

            // Verify ownership
            if (complaint.UserId != userId) return null;

            // Customer can cancel when: pending, under_review, or approved
            var cancellableStatuses = new[] { ComplaintStatus.Pending, ComplaintStatus.UnderReview, ComplaintStatus.Approved };
            if (!cancellableStatuses.Contains(complaint.Status?.ToLower() ?? "")) return null;

            complaint.Status = ComplaintStatus.Cancelled;
            complaint.CancelledByCustomer = true;
            return await _complaintRepository.UpdateAsync(complaint);
        }

        public bool CanCustomerModify(ComplaintRequest complaint)
        {
            if (string.IsNullOrEmpty(complaint.Status))
            {
                return true;
            }

            return ModifiableStatuses.Contains(complaint.Status.ToLower());
        }

        public async Task<(bool IsValid, string? Error)> ValidateOrderItemOwnershipAsync(Guid orderItemId, Guid userId)
        {
            // Find the order item
            var orderItems = await _orderItemRepository.SearchAsyncInclude(
                oi => oi.OrderItemId == orderItemId,
                oi => oi.Order);
            var orderItem = orderItems.FirstOrDefault();

            if (orderItem == null)
            {
                return (false, "Order item not found");
            }

            // Verify the order item belongs to the user's order
            if (orderItem.Order == null)
            {
                return (false, "Order not found for this order item");
            }

            if (orderItem.Order.UserId != userId)
            {
                return (false, "This order item does not belong to your order");
            }

            // Verify the order is in a delivered status (customer must have received the item)
            if (orderItem.Order.Status == null ||
                !orderItem.Order.Status.Equals("delivered", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "You can only file a complaint for delivered orders");
            }

            return (true, null);
        }

        public bool IsValidStatusTransition(string currentStatus, string newStatus)
        {
            currentStatus = currentStatus.ToLower();
            newStatus = newStatus.ToLower();

            if (!ValidStatusTransitions.ContainsKey(currentStatus))
            {
                return false;
            }

            return ValidStatusTransitions[currentStatus].Contains(newStatus);
        }

        #endregion
    }
}
