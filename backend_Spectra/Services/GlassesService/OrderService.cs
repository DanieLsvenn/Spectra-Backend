using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Repositories.Basic;
using Repositories.ModelExtensions;
using Repositories.Models;

namespace Services.GlassesService
{
    public class OrderValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public interface IOrderService
    {
        // Create operations
        Task<Order> CreateOrderAsync(Order order, List<OrderItem> orderItems);
        Task<OrderValidationResult> ValidateOrderItemsAsync(List<OrderItem> orderItems, Guid userId);

        // Read operations
        Task<Order?> GetOrderByIdAsync(Guid orderId);
        Task<Order?> GetOrderByIdWithDetailsAsync(Guid orderId);
        Task<List<Order>> GetOrdersByUserAsync(Guid userId);
        Task<PaginationResult<Order>> GetOrdersByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10);
        Task<PaginationResult<Order>> GetAllOrdersAsync(int currentPage = 1, int pageSize = 10);

        // Update operations
        Task<Order?> UpdateOrderStatusAsync(Guid orderId, string newStatus, string userRole, Guid userId);
        Task<bool> CanModifyOrderAsync(Guid orderId);

        // Price calculation
        Task<double> CalculateOrderTotalAsync(List<OrderItem> orderItems);
        Task<double> CalculateItemPriceAsync(OrderItem item);
    }

    public class OrderService : IOrderService
    {
        private readonly GenericRepository<Order> _orderRepository;
        private readonly GenericRepository<OrderItem> _orderItemRepository;
        private readonly GenericRepository<Frame> _frameRepository;
        private readonly GenericRepository<LensType> _lensTypeRepository;
        private readonly GenericRepository<LensFeature> _lensFeatureRepository;
        private readonly GenericRepository<Prescription> _prescriptionRepository;
        private readonly GenericRepository<Payment> _paymentRepository;

        // Order statuses
        public static class OrderStatus
        {
            public const string Pending = "pending";
            public const string Confirmed = "confirmed";
            public const string Processing = "processing";
            public const string Shipped = "shipped";
            public const string Delivered = "delivered";
            public const string Cancelled = "cancelled";
        }

        // Valid status transitions
        private static readonly Dictionary<string, string[]> ValidStatusTransitions = new()
        {
            { OrderStatus.Pending, new[] { OrderStatus.Confirmed, OrderStatus.Cancelled } },
            { OrderStatus.Confirmed, new[] { OrderStatus.Processing, OrderStatus.Cancelled } },
            { OrderStatus.Processing, new[] { OrderStatus.Shipped, OrderStatus.Cancelled } },
            { OrderStatus.Shipped, new[] { OrderStatus.Delivered } },
            { OrderStatus.Delivered, Array.Empty<string>() },
            { OrderStatus.Cancelled, Array.Empty<string>() }
        };

        public OrderService(
            GenericRepository<Order> orderRepository,
            GenericRepository<OrderItem> orderItemRepository,
            GenericRepository<Frame> frameRepository,
            GenericRepository<LensType> lensTypeRepository,
            GenericRepository<LensFeature> lensFeatureRepository,
            GenericRepository<Prescription> prescriptionRepository,
            GenericRepository<Payment> paymentRepository)
        {
            _orderRepository = orderRepository;
            _orderItemRepository = orderItemRepository;
            _frameRepository = frameRepository;
            _lensTypeRepository = lensTypeRepository;
            _lensFeatureRepository = lensFeatureRepository;
            _prescriptionRepository = prescriptionRepository;
            _paymentRepository = paymentRepository;
        }

        #region Create Operations

        public async Task<Order> CreateOrderAsync(Order order, List<OrderItem> orderItems)
        {
            // Set order defaults
            order.OrderId = Guid.NewGuid();
            order.Status = OrderStatus.Pending;
            order.CreatedAt = DateTime.UtcNow;

            // Calculate total amount
            order.TotalAmount = await CalculateOrderTotalAsync(orderItems);

            // Create the order
            var createdOrder = await _orderRepository.CreateAsync(order);

            // Create order items
            foreach (var item in orderItems)
            {
                item.OrderItemId = Guid.NewGuid();
                item.OrderId = createdOrder.OrderId;
                item.OrderPrice = await CalculateItemPriceAsync(item);
                await _orderItemRepository.CreateAsync(item);
            }

            // Return order with items
            return await GetOrderByIdWithDetailsAsync(createdOrder.OrderId) ?? createdOrder;
        }

        public async Task<OrderValidationResult> ValidateOrderItemsAsync(List<OrderItem> orderItems, Guid userId)
        {
            var result = new OrderValidationResult { IsValid = true };

            if (orderItems == null || !orderItems.Any())
            {
                result.IsValid = false;
                result.Errors.Add("Order must contain at least one item");
                return result;
            }

            foreach (var item in orderItems)
            {
                // Validate frame exists and is available
                if (item.FrameId.HasValue)
                {
                    var frames = await _frameRepository.SearchAsync(f => f.FrameId == item.FrameId);
                    var frame = frames.FirstOrDefault();

                    if (frame == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame with ID {item.FrameId} not found");
                        continue;
                    }

                    if (frame.Status?.ToLower() != "available")
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame '{frame.FrameName}' is not available");
                        continue;
                    }

                    // Validate selectedColor when frame color is NULL
                    if (string.IsNullOrEmpty(frame.Color) && string.IsNullOrEmpty(item.SelectedColor))
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Frame '{frame.FrameName}' requires a color selection");
                    }
                }
                else
                {
                    result.IsValid = false;
                    result.Errors.Add("Each order item must have a frame");
                }

                // Validate lens type
                if (item.LensTypeId.HasValue)
                {
                    var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == item.LensTypeId);
                    var lensType = lensTypes.FirstOrDefault();

                    if (lensType == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Lens type with ID {item.LensTypeId} not found");
                        continue;
                    }

                    // Validate prescription requirement
                    if (lensType.RequiresPrescription == true)
                    {
                        if (!item.PrescriptionId.HasValue)
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Lens type '{lensType.LensSpecification}' requires a prescription");
                            continue;
                        }

                        // Validate prescription exists and belongs to user
                        var prescriptions = await _prescriptionRepository.SearchAsync(p => p.PrescriptionId == item.PrescriptionId);
                        var prescription = prescriptions.FirstOrDefault();

                        if (prescription == null)
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Prescription with ID {item.PrescriptionId} not found");
                        }
                        else if (prescription.UserId != userId)
                        {
                            result.IsValid = false;
                            result.Errors.Add("Prescription does not belong to the current user");
                        }
                        else if (prescription.ExpirationDate.HasValue && prescription.ExpirationDate < DateTime.UtcNow)
                        {
                            result.IsValid = false;
                            result.Errors.Add("Prescription has expired");
                        }
                    }
                }

                // Validate lens feature if provided
                if (item.FeatureId.HasValue)
                {
                    var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                    var feature = features.FirstOrDefault();

                    if (feature == null)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Lens feature with ID {item.FeatureId} not found");
                    }
                }

                // Validate quantity
                if (!item.Quantity.HasValue || item.Quantity <= 0)
                {
                    result.IsValid = false;
                    result.Errors.Add("Each order item must have a quantity greater than 0");
                }
            }

            return result;
        }

        #endregion

        #region Read Operations

        public async Task<Order?> GetOrderByIdAsync(Guid orderId)
        {
            var orders = await _orderRepository.SearchAsync(o => o.OrderId == orderId);
            return orders.FirstOrDefault();
        }

        public async Task<Order?> GetOrderByIdWithDetailsAsync(Guid orderId)
        {
            var orders = await _orderRepository.SearchAsyncInclude(
                o => o.OrderId == orderId,
                o => o.OrderItems,
                o => o.User,
                o => o.Payments
            );
            
            var order = orders.FirstOrDefault();
            
            if (order != null)
            {
                // Load related data for each order item
                foreach (var item in order.OrderItems)
                {
                    if (item.FrameId.HasValue)
                    {
                        var frames = await _frameRepository.SearchAsync(f => f.FrameId == item.FrameId);
                        item.Frame = frames.FirstOrDefault();
                    }
                    if (item.LensTypeId.HasValue)
                    {
                        var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == item.LensTypeId);
                        item.LensType = lensTypes.FirstOrDefault();
                    }
                    if (item.FeatureId.HasValue)
                    {
                        var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                        item.Feature = features.FirstOrDefault();
                    }
                    if (item.PrescriptionId.HasValue)
                    {
                        var prescriptions = await _prescriptionRepository.SearchAsync(p => p.PrescriptionId == item.PrescriptionId);
                        item.Prescription = prescriptions.FirstOrDefault();
                    }
                }
            }
            
            return order;
        }

        public async Task<List<Order>> GetOrdersByUserAsync(Guid userId)
        {
            var orders = await _orderRepository.SearchAsyncInclude(
                o => o.UserId == userId,
                o => o.OrderItems,
                o => o.Payments
            );
            return orders.ToList();
        }

        public async Task<PaginationResult<Order>> GetOrdersByUserAsync(Guid userId, int currentPage = 1, int pageSize = 10)
        {
            return await _orderRepository.SearchWithPagingAsyncIncludeOrderBy(
                o => o.UserId == userId,
                currentPage,
                pageSize,
                orderBy: o => o.CreatedAt,
                ascending: false,
                o => o.OrderItems,
                o => o.Payments
            );
        }

        public async Task<PaginationResult<Order>> GetAllOrdersAsync(int currentPage = 1, int pageSize = 10)
        {
            return await _orderRepository.SearchWithPagingAsyncIncludeOrderBy(
                o => true,
                currentPage,
                pageSize,
                orderBy: o => o.CreatedAt,
                ascending: false,
                o => o.OrderItems,
                o => o.User,
                o => o.Payments
            );
        }

        #endregion

        #region Update Operations

        public async Task<Order?> UpdateOrderStatusAsync(Guid orderId, string newStatus, string userRole, Guid userId)
        {
            var order = await GetOrderByIdAsync(orderId);

            if (order == null)
            {
                return null;
            }

            var currentStatus = order.Status?.ToLower() ?? OrderStatus.Pending;
            newStatus = newStatus.ToLower();

            // Validate status transition
            if (!ValidStatusTransitions.ContainsKey(currentStatus) ||
                !ValidStatusTransitions[currentStatus].Contains(newStatus))
            {
                return null;
            }

            // Role-based permissions
            switch (userRole.ToLower())
            {
                case "customer":
                    // Customer cannot modify orders after creation
                    return null;

                case "staff":
                    // Staff can update shipping status (processing, shipped, delivered)
                    var staffAllowedStatuses = new[] { OrderStatus.Processing, OrderStatus.Shipped, OrderStatus.Delivered };
                    if (!staffAllowedStatuses.Contains(newStatus))
                    {
                        return null;
                    }
                    break;

                case "manager":
                case "admin":
                    // Manager/Admin can do all transitions including cancel
                    break;

                default:
                    return null;
            }

            order.Status = newStatus;

            // Set arrival date when delivered
            if (newStatus == OrderStatus.Delivered)
            {
                order.ArrivalDate = DateTime.UtcNow;
            }

            return await _orderRepository.UpdateAsync(order);
        }

        public async Task<bool> CanModifyOrderAsync(Guid orderId)
        {
            // Check if order has been paid
            var payments = await _paymentRepository.SearchAsync(p => 
                p.OrderId == orderId && 
                p.PaymentStatus != null && p.PaymentStatus.ToLower() == "completed");

            return !payments.Any();
        }

        #endregion

        #region Price Calculation

        public async Task<double> CalculateOrderTotalAsync(List<OrderItem> orderItems)
        {
            double total = 0;

            foreach (var item in orderItems)
            {
                var itemPrice = await CalculateItemPriceAsync(item);
                total += itemPrice * (item.Quantity ?? 1);
            }

            return total;
        }

        public async Task<double> CalculateItemPriceAsync(OrderItem item)
        {
            double basePrice = 0;
            double lensTypePrice = 0;
            double featurePrice = 0;

            // Get frame base price
            if (item.FrameId.HasValue)
            {
                var frames = await _frameRepository.SearchAsync(f => f.FrameId == item.FrameId);
                var frame = frames.FirstOrDefault();
                basePrice = frame?.BasePrice ?? 0;
            }

            // Get lens type extra price
            if (item.LensTypeId.HasValue)
            {
                var lensTypes = await _lensTypeRepository.SearchAsync(lt => lt.LensTypeId == item.LensTypeId);
                var lensType = lensTypes.FirstOrDefault();
                lensTypePrice = lensType?.ExtraPrice ?? 0;
            }

            // Get lens feature extra price
            if (item.FeatureId.HasValue)
            {
                var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                var feature = features.FirstOrDefault();
                featurePrice = feature?.ExtraPrice ?? 0;
            }

            return basePrice + lensTypePrice + featurePrice;
        }

        #endregion
    }
}
