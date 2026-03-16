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
        Task<Order?> UpdateOrderShippingAsync(Guid orderId, string shippingMethod, double shippingFee, double totalAmount);
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
        private readonly GenericRepository<FrameColor> _frameColorRepository;
        private readonly GenericRepository<FrameLensType> _frameLensTypeRepository;
        private readonly GenericRepository<LensType> _lensTypeRepository;
        private readonly GenericRepository<LensFeature> _lensFeatureRepository;
        private readonly GenericRepository<LensIndex> _lensIndexRepository;
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
            GenericRepository<FrameColor> frameColorRepository,
            GenericRepository<FrameLensType> frameLensTypeRepository,
            GenericRepository<LensType> lensTypeRepository,
            GenericRepository<LensFeature> lensFeatureRepository,
            GenericRepository<LensIndex> lensIndexRepository,
            GenericRepository<Prescription> prescriptionRepository,
            GenericRepository<Payment> paymentRepository)
        {
            _orderRepository = orderRepository;
            _orderItemRepository = orderItemRepository;
            _frameRepository = frameRepository;
            _frameColorRepository = frameColorRepository;
            _frameLensTypeRepository = frameLensTypeRepository;
            _lensTypeRepository = lensTypeRepository;
            _lensFeatureRepository = lensFeatureRepository;
            _lensIndexRepository = lensIndexRepository;
            _prescriptionRepository = prescriptionRepository;
            _paymentRepository = paymentRepository;
        }

        #region Create Operations

        public async Task<Order> CreateOrderAsync(Order order, List<OrderItem> orderItems)
        {
            // Set order defaults
            order.OrderId = Guid.NewGuid();
            order.Status = OrderStatus.Pending;
            order.CreatedAt = TimeHelper.Now;

            // Calculate total amount
            order.TotalAmount = await CalculateOrderTotalAsync(orderItems);

            // Create the order
            var createdOrder = await _orderRepository.CreateAsync(order);

            // Create order items and deduct stock
            foreach (var item in orderItems)
            {
                item.OrderItemId = Guid.NewGuid();
                item.OrderId = createdOrder.OrderId;
                item.UnitPrice = await CalculateItemPriceAsync(item);
                await _orderItemRepository.CreateAsync(item);

                // Deduct stock per color variant
                if (item.FrameId.HasValue && item.SelectedColorId.HasValue)
                {
                    await DeductVariantStockAsync(item.FrameId.Value, item.SelectedColorId.Value, item.Quantity ?? 1);
                }
                else if (item.FrameId.HasValue)
                {
                    await DeductFrameStockAsync(item.FrameId.Value, item.Quantity ?? 1);
                }
            }

            // Return order with items
            return await GetOrderByIdWithDetailsAsync(createdOrder.OrderId) ?? createdOrder;
        }

        private async Task DeductFrameStockAsync(Guid frameId, int quantity)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();
            
            if (frame != null)
            {
                var currentStock = frame.StockQuantity ?? 0;
                frame.StockQuantity = Math.Max(0, currentStock - quantity);
                
                // Auto-update status if out of stock
                if (frame.StockQuantity <= 0)
                {
                    frame.Status = "out_of_stock";
                }
                
                await _frameRepository.UpdateAsync(frame);
            }
        }

        private async Task DeductVariantStockAsync(Guid frameId, Guid colorId, int quantity)
        {
            var variants = await _frameColorRepository.SearchAsync(
                fc => fc.FrameId == frameId && fc.ColorId == colorId);
            var variant = variants.FirstOrDefault();

            if (variant != null)
            {
                var currentStock = variant.StockQuantity ?? 0;
                variant.StockQuantity = Math.Max(0, currentStock - quantity);
                await _frameColorRepository.UpdateAsync(variant);
            }

            // Also update frame-level stock
            await RecalculateFrameStockAsync(frameId);
        }

        private async Task RestoreFrameStockAsync(Guid frameId, int quantity)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();
            
            if (frame != null)
            {
                var currentStock = frame.StockQuantity ?? 0;
                frame.StockQuantity = currentStock + quantity;
                
                // Auto-update status if back in stock
                if (frame.StockQuantity > 0 && frame.Status?.ToLower() == "out_of_stock")
                {
                    frame.Status = "available";
                }
                
                await _frameRepository.UpdateAsync(frame);
            }
        }

        private async Task RestoreVariantStockAsync(Guid frameId, Guid colorId, int quantity)
        {
            var variants = await _frameColorRepository.SearchAsync(
                fc => fc.FrameId == frameId && fc.ColorId == colorId);
            var variant = variants.FirstOrDefault();

            if (variant != null)
            {
                var currentStock = variant.StockQuantity ?? 0;
                variant.StockQuantity = currentStock + quantity;
                await _frameColorRepository.UpdateAsync(variant);
            }

            // Also update frame-level stock
            await RecalculateFrameStockAsync(frameId);
        }

        private async Task RecalculateFrameStockAsync(Guid frameId)
        {
            var frames = await _frameRepository.SearchAsync(f => f.FrameId == frameId);
            var frame = frames.FirstOrDefault();
            if (frame == null) return;

            var allVariants = await _frameColorRepository.SearchAsync(fc => fc.FrameId == frameId);
            var totalStock = allVariants.Sum(v => v.StockQuantity ?? 0);

            frame.StockQuantity = totalStock;

            if (totalStock <= 0)
            {
                frame.Status = "out_of_stock";
            }
            else if (frame.Status?.ToLower() == "out_of_stock")
            {
                frame.Status = "available";
            }

            await _frameRepository.UpdateAsync(frame);
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
                Frame frame = null;

                // Validate frame exists and is available
                if (item.FrameId.HasValue)
                {
                    var frames = await _frameRepository.SearchAsyncInclude(
                        f => f.FrameId == item.FrameId,
                        f => f.FrameColors);
                    frame = frames.FirstOrDefault();

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

                    var requestedQuantity = item.Quantity ?? 1;

                    // Validate per-color variant stock if a color is selected
                    if (item.SelectedColorId.HasValue)
                    {
                        var variant = frame.FrameColors.FirstOrDefault(
                            fc => fc.ColorId == item.SelectedColorId);

                        if (variant == null)
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Color is not available for frame '{frame.FrameName}'");
                            continue;
                        }

                        var variantStock = variant.StockQuantity ?? 0;
                        if (variantStock < requestedQuantity)
                        {
                            result.IsValid = false;
                            if (variantStock <= 0)
                            {
                                result.Errors.Add($"Frame '{frame.FrameName}' in the selected color is out of stock. Please choose a different color or use Preorder.");
                            }
                            else
                            {
                                result.Errors.Add($"Frame '{frame.FrameName}' in the selected color only has {variantStock} in stock, but {requestedQuantity} requested");
                            }
                            continue;
                        }
                    }
                    else
                    {
                        // No color selected - validate frame-level stock
                        var availableStock = frame.StockQuantity ?? 0;
                        if (availableStock < requestedQuantity)
                        {
                            result.IsValid = false;
                            if (availableStock <= 0)
                            {
                                result.Errors.Add($"Frame '{frame.FrameName}' is out of stock. Please use Preorder instead.");
                            }
                            else
                            {
                                result.Errors.Add($"Frame '{frame.FrameName}' only has {availableStock} in stock, but {requestedQuantity} requested");
                            }
                            continue;
                        }

                        // Require color selection if frame has colors defined
                        if (frame.FrameColors.Any())
                        {
                            result.IsValid = false;
                            result.Errors.Add($"Frame '{frame.FrameName}' requires a color selection. Available colors have individual stock levels.");
                            continue;
                        }
                    }
                }
                else
                {
                    result.IsValid = false;
                    result.Errors.Add("Each order item must have a frame");
                    continue;
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

                    // Validate that the lens type is supported by this frame
                    if (frame != null)
                    {
                        var supportedLensTypes = await _frameLensTypeRepository.SearchAsync(
                            flt => flt.FrameId == frame.FrameId);
                        var supportedIds = supportedLensTypes
                            .Where(flt => flt.LensTypeId.HasValue)
                            .Select(flt => flt.LensTypeId!.Value)
                            .ToHashSet();

                        // If the frame has supported lens types defined, validate against them
                        // Single Vision and Non-Prescription are always allowed
                        if (supportedIds.Any())
                        {
                            var isAlwaysAllowed = lensType.RequiresPrescription != true;
                            if (!isAlwaysAllowed && !supportedIds.Contains(lensType.LensTypeId))
                            {
                                result.IsValid = false;
                                result.Errors.Add($"Frame '{frame.FrameName}' does not support lens type '{lensType.LensSpecification}'. Please check the frame's supported lens types.");
                                continue;
                            }
                        }
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
                        else if (prescription.ExpirationDate.HasValue && prescription.ExpirationDate < TimeHelper.Now)
                        {
                            result.IsValid = false;
                            result.Errors.Add("Prescription has expired");
                        }
                        else if (frame != null)
                        {
                            // Validate prescription values against frame Rx/PD limits
                            ValidatePrescriptionAgainstFrame(result, frame, prescription);
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

        /// <summary>
        /// Validates a prescription's sphere and PD values against the frame's supported Rx/PD limits.
        /// </summary>
        private void ValidatePrescriptionAgainstFrame(OrderValidationResult result, Frame frame, Prescription prescription)
        {
            // Validate Sphere (Rx) limits
            if (frame.MinRx.HasValue || frame.MaxRx.HasValue)
            {
                var sphereValues = new List<double?> { prescription.SphereLeft, prescription.SphereRight };
                foreach (var sphere in sphereValues.Where(s => s.HasValue))
                {
                    if (frame.MinRx.HasValue && sphere < frame.MinRx)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription sphere value ({sphere}) is below the frame's minimum supported Rx ({frame.MinRx}). This frame cannot support your prescription.");
                        return;
                    }
                    if (frame.MaxRx.HasValue && sphere > frame.MaxRx)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription sphere value ({sphere}) exceeds the frame's maximum supported Rx ({frame.MaxRx}). This frame cannot support your prescription.");
                        return;
                    }
                }
            }

            // Validate Pupillary Distance (PD) limits
            if (frame.MinPd.HasValue || frame.MaxPd.HasValue)
            {
                if (prescription.PupillaryDistance.HasValue)
                {
                    var pd = prescription.PupillaryDistance.Value;
                    if (frame.MinPd.HasValue && pd < frame.MinPd)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription PD ({pd}mm) is below the frame's minimum supported PD ({frame.MinPd}mm). This frame cannot fit your pupillary distance.");
                    }
                    if (frame.MaxPd.HasValue && pd > frame.MaxPd)
                    {
                        result.IsValid = false;
                        result.Errors.Add($"Prescription PD ({pd}mm) exceeds the frame's maximum supported PD ({frame.MaxPd}mm). This frame cannot fit your pupillary distance.");
                    }
                }
            }
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
                order.ArrivalDate = TimeHelper.Now;
            }

            // Restore stock when order is cancelled
            if (newStatus == OrderStatus.Cancelled)
            {
                // Get order items to restore stock
                var orderWithItems = await GetOrderByIdWithDetailsAsync(orderId);
                if (orderWithItems != null)
                {
                    foreach (var item in orderWithItems.OrderItems)
                    {
                        if (item.FrameId.HasValue && item.SelectedColorId.HasValue)
                        {
                            await RestoreVariantStockAsync(item.FrameId.Value, item.SelectedColorId.Value, item.Quantity ?? 1);
                        }
                        else if (item.FrameId.HasValue)
                        {
                            await RestoreFrameStockAsync(item.FrameId.Value, item.Quantity ?? 1);
                        }
                    }
                }
            }

            return await _orderRepository.UpdateAsync(order);
        }

        public async Task<Order?> UpdateOrderShippingAsync(Guid orderId, string shippingMethod, double shippingFee, double totalAmount)
        {
            var order = await GetOrderByIdAsync(orderId);

            if (order == null)
            {
                return null;
            }

            // Update shipping information
            order.ShippingMethod = shippingMethod;
            order.ShippingFee = shippingFee;
            order.TotalAmount = totalAmount;

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
            double lensIndexPrice = 0;

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
                lensTypePrice = lensType?.BasePrice ?? 0;
            }

            // Get lens feature extra price
            if (item.FeatureId.HasValue)
            {
                var features = await _lensFeatureRepository.SearchAsync(f => f.FeatureId == item.FeatureId);
                var feature = features.FirstOrDefault();
                featurePrice = feature?.ExtraPrice ?? 0;
            }

            // Get lens index additional price
            if (item.LensIndexId.HasValue)
            {
                var lensIndices = await _lensIndexRepository.SearchAsync(li => li.LensIndexId == item.LensIndexId);
                var lensIndex = lensIndices.FirstOrDefault();
                lensIndexPrice = lensIndex?.AdditionalPrice ?? 0;
            }

            return basePrice + lensTypePrice + featurePrice + lensIndexPrice;
        }

        #endregion
    }
}
